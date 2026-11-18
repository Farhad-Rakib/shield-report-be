using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.ScanTools;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;
using ShieldReport.Scanning.Configuration;

namespace ShieldReport.Scanning.Runners;

public sealed partial class DockerScanRunner : IScanRunner
{
    // Strict allowlist for a URL/hostname/IP/CIDR target — alnum plus the handful of
    // characters those shapes legitimately need. Excludes every shell metacharacter
    // (;, &, |, `, $, etc.) by construction, not just by a denylist check.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.\-:/_]{0,253}$")]
    private static partial Regex TargetAllowlistRegex();

    private static readonly char[] ShellMetacharacters = [';', '&', '|', '`', '$', '\n', '\r', '<', '>'];

    // reconftw's stdout is progress logging, not results (those are read from its output files,
    // see ReadReconftwResults), so only its tail is worth keeping in memory / in Scan.RawOutput.
    // Naabu/Nuclei stdout IS the result stream (JSONL), so it is never truncated.
    private const int ReconftwStdoutCapChars = 1_000_000;
    private const int StderrCapChars = 64_000;

    private readonly ScanToolSettingsProvider _settingsProvider;
    private readonly IScanToolSettingsService _toolSettings;

    public DockerScanRunner(ScanToolSettingsProvider settingsProvider, IScanToolSettingsService toolSettings)
    {
        _settingsProvider = settingsProvider;
        _toolSettings = toolSettings;
    }

    public const string ScanContainerLabel = "shieldreport.scan-id";

    public static string ContainerNameFor(long scanId) => $"shieldreport-scan-{scanId}";

    // Runs (or re-attaches to) the scan's container and follows it to the end.
    //   stopToken   — user Stop or timeout: the container is stopped and removed.
    //   detachToken — API shutdown / lost lease: only stop *following*; the container keeps
    //                 running and a later claim re-attaches (Outcome = Detached).
    // `reattach` is true when the scan was already Running (claimed after its previous owner died):
    // the existing container is followed instead of starting a new one, and its absence means the
    // run is lost (Outcome = ContainerMissing).
    public async Task<ScanRunResult> RunAsync(Scan scan, ClientAsset asset, ScanToolRuntimeSettings settings, bool reattach, Func<string, string, Task> onOutputLine, CancellationToken stopToken, CancellationToken detachToken)
    {
        var target = asset.Identifier;
        if (!TargetAllowlistRegex().IsMatch(target) || target.IndexOfAny(ShellMetacharacters) >= 0)
        {
            return ScanRunResult.Failed(string.Empty, $"Target '{target}' failed the allowlist validation — refusing to invoke the scan container.");
        }

        var containerName = ContainerNameFor(scan.Id);

        try
        {
            var state = await InspectContainerAsync(containerName);
            if (state is null)
            {
                if (reattach)
                {
                    DeleteReconftwOutput(scan);
                    return new ScanRunResult(ScanRunOutcome.ContainerMissing, string.Empty, "Interrupted: the scan container no longer exists (host restart or manual removal). Please run the scan again.");
                }

                // Reconftw's share of the DNS budget depends on how many may run at once (live setting).
                var reconftwParallel = scan.Tool == ScanTool.Reconftw
                    ? Math.Max(1, await _toolSettings.GetMaxParallelAsync(ScanTool.Reconftw, stopToken))
                    : 1;
                var toolArgs = BuildToolArguments(scan, target, settings, reconftwParallel);
                var create = await RunDockerCommandAsync(BuildRunArguments(scan, settings, containerName, settings.Image, toolArgs), TimeSpan.FromMinutes(10));
                if (create.ExitCode != 0)
                {
                    return ScanRunResult.Failed(string.Empty, $"Failed to start the scan container (docker exit {create.ExitCode}): {create.StdErr.Trim()}");
                }
            }

            using var followCts = CancellationTokenSource.CreateLinkedTokenSource(stopToken, detachToken);
            var followed = await FollowLogsAsync(containerName, onOutputLine, followCts.Token);

            if (!followed)
            {
                if (stopToken.IsCancellationRequested)
                {
                    await StopContainerAsync(containerName);
                    await CleanupAsync(scan);
                    // The caller (ScanWorkerBackgroundService) decides whether this was a user
                    // Stop or a timeout — it owns both cancellation sources.
                    return new ScanRunResult(ScanRunOutcome.Stopped, string.Empty, "Stopped.");
                }

                return new ScanRunResult(ScanRunOutcome.Detached, string.Empty, null);
            }

            // `docker logs -f` returned, so the container has exited. Collect the complete output
            // (also covers lines produced while no process was following, e.g. during a restart).
            // The container and reconftw's output folder are NOT removed here: the worker calls
            // CleanupAsync only after it has saved the result, so a crash in between can re-attach
            // to the exited container and collect it again.
            var (rawOutput, errorOutput) = await CollectLogsAsync(containerName, scan.Tool);
            var finalState = await InspectContainerAsync(containerName);

            if (finalState is null)
            {
                return ScanRunResult.Failed(rawOutput, "The scan container disappeared before its result could be read.");
            }

            if (finalState.Value.ExitCode != 0)
            {
                var exitMessage = finalState.Value.OomKilled || finalState.Value.ExitCode == 137
                    ? $"Scan container was killed (exit {finalState.Value.ExitCode}): it most likely exceeded its {settings.MemoryMb} MB memory limit."
                    : $"docker exited with code {finalState.Value.ExitCode}.";
                return ScanRunResult.Failed(rawOutput, errorOutput.Length > 0 ? $"{exitMessage}\n{errorOutput}" : exitMessage);
            }

            var structuredOutput = scan.Tool == ScanTool.Reconftw ? ReadReconftwResults(scan) : null;
            return new ScanRunResult(ScanRunOutcome.Completed, rawOutput, null, structuredOutput);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException)
        {
            return ScanRunResult.Failed(string.Empty, $"Failed to invoke docker: {ex.Message}");
        }
    }

    private List<string> BuildRunArguments(Scan scan, ScanToolRuntimeSettings settings, string containerName, string image, IReadOnlyList<string> toolArgs)
    {
        var args = new List<string>();
        args.Add("run");
        // Detached (-d) and NOT --rm: the container must outlive this process (API restart) and
        // keep its exit code + logs until the worker has collected them; RemoveContainerAsync
        // deletes it afterwards, and ScanContainerJanitorService sweeps any leftovers.
        args.Add("-d");
        args.Add("--name");
        args.Add(containerName);
        args.Add("--label");
        args.Add($"{ScanContainerLabel}={scan.Id}");
        // six2dez/reconftw:latest owns /reconftw as root and its script does git operations
        // there — running as UID 1000 trips git's "dubious ownership" check and then fails to
        // mkdir its own output dir (Permission denied), confirmed against the real image.
        // Naabu/Nuclei have no such requirement, so only Reconftw runs as the image's default
        // (root) user; the container is still locked down via no-new-privileges.
        // Naabu SYN scans need raw sockets, i.e. root inside the container (Docker's default
        // capability set already includes NET_RAW); connect scans run unprivileged as UID 1000.
        var naabuSyn = scan.Tool == ScanTool.Naabu && settings.Naabu?.ScanType == "syn";
        // Custom tools run as UID 1000 unless the admin explicitly marked the tool "run as root".
        var customRoot = scan.Tool == ScanTool.Custom && settings.Custom?.RunAsRoot == true;
        if (scan.Tool != ScanTool.Reconftw && !naabuSyn && !customRoot)
        {
            args.Add("--user");
            args.Add("1000:1000");
        }
        else if (scan.Tool == ScanTool.Reconftw)
        {
            // six2dez/reconftw:latest only publishes a linux/arm64 manifest on Docker Hub (no
            // linux/amd64 entry at all — confirmed via `docker manifest inspect`), so without
            // this flag `docker run` fails outright on amd64 hosts with "no matching manifest
            // for linux/amd64". Forcing arm64 runs natively on arm64 hosts and via QEMU
            // emulation on amd64 hosts. Matches the platform pin on reconftw-image in
            // docker-compose.yml.
            args.Add("--platform");
            args.Add("linux/arm64");
        }
        args.Add("--security-opt");
        args.Add("no-new-privileges");
        // Hard resource ceiling per scan container: if a tool exceeds it, the kernel OOM-kills
        // only this container (surfaced as a failed scan) instead of starving SQL Server / the API
        // on a small host. Values come from the scan's settings snapshot (Scan Tools page).
        args.Add("--memory");
        args.Add($"{settings.MemoryMb}m");
        args.Add("--memory-swap");
        args.Add($"{settings.MemoryMb + 512}m");
        args.Add("--cpus");
        args.Add(settings.Cpus.ToString(System.Globalization.CultureInfo.InvariantCulture));
        args.Add("--pids-limit");
        args.Add(settings.PidsLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (scan.Tool == ScanTool.Reconftw)
        {
            // reconftw writes its real results (subdomains.txt, takeover.txt, ...) to files, not
            // stdout. Point its -o output at a per-scan folder on a named volume that the API
            // container also mounts, so ReadReconftwResults can pick them up after the run.
            args.Add("-v");
            args.Add($"{_settingsProvider.ReconftwOutputVolume}:/scan-output");
        }
        // UID 1000 has no /etc/passwd entry in these images, so $HOME defaults to "/" and
        // tools that write a config/cache dir (e.g. nuclei) fail with a permission error —
        // confirmed against the real image. /tmp is world-writable regardless of UID.
        // Not for Reconftw: it runs as root and its config resolves everything from $HOME
        // (tools=$HOME/Tools, GOPATH=$HOME/go, resolvers) — with HOME=/tmp none of its tools or
        // resolver lists are found (verified inside the image), so it must keep HOME=/root.
        if (scan.Tool != ScanTool.Reconftw)
        {
            args.Add("--env");
            args.Add("HOME=/tmp");
        }
        if (scan.Tool == ScanTool.Nuclei)
        {
            // Without a persistent cache, every single Nuclei run re-downloads the entire
            // 10,000+ template set from GitHub into the ephemeral container's throwaway
            // filesystem — confirmed to hang/fail outright under transient network trouble or
            // GitHub rate-limiting after repeated runs in a short window. This named volume
            // (created + chmod'd 777 once by the nuclei-templates-init service in
            // docker-compose.yml, since a fresh volume is root-owned and UID 1000 can't write to
            // it otherwise) persists the templates across every scan, so only the first run ever
            // has to download anything.
            args.Add("-v");
            args.Add("nuclei-templates:/tmp/nuclei-templates");
        }
        args.Add(image);
        foreach (var arg in toolArgs)
        {
            args.Add(arg);
        }

        return args;
    }

    // Streams the container's stdout/stderr to the live console until it exits. Returns true when
    // the container finished (logs ended on their own), false when following was cancelled.
    private static async Task<bool> FollowLogsAsync(string containerName, Func<string, string, Task> onOutputLine, CancellationToken cancellationToken)
    {
        var startInfo = DockerStartInfo("logs", "-f", containerName);
        using var process = new Process { StartInfo = startInfo };
        // Many tools (nuclei in particular, once -silent suppresses its stdout banner) write their
        // progress/status logging to stderr — stream both so the console reflects real activity.
        process.OutputDataReceived += async (_, e) =>
        {
            if (e.Data is not null)
            {
                await SafeOnOutputLineAsync(onOutputLine, e.Data, "stdout");
            }
        };
        process.ErrorDataReceived += async (_, e) =>
        {
            if (e.Data is not null)
            {
                await SafeOnOutputLineAsync(onOutputLine, e.Data, "stderr");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            // Stops following only — the container itself is untouched here.
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Already gone.
            }

            return false;
        }
    }

    // Full, non-follow read of the finished container's logs. reconftw's stdout is progress
    // logging (its results come from files), so only its tail is kept; Naabu/Nuclei stdout IS the
    // JSONL result stream and is kept whole.
    private static async Task<(string RawOutput, string ErrorOutput)> CollectLogsAsync(string containerName, ScanTool tool)
    {
        var rawOutput = new StringBuilder();
        var errorOutput = new StringBuilder();
        var startInfo = DockerStartInfo("logs", containerName);
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            rawOutput.AppendLine(e.Data);
            if (tool == ScanTool.Reconftw)
            {
                KeepTail(rawOutput, ReconftwStdoutCapChars);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            errorOutput.AppendLine(e.Data);
            KeepTail(errorOutput, StderrCapChars);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5));
        return (rawOutput.ToString(), errorOutput.ToString());
    }

    private readonly record struct ContainerState(string Status, int ExitCode, bool OomKilled);

    private static async Task<ContainerState?> InspectContainerAsync(string containerName)
    {
        var result = await RunDockerCommandAsync(["inspect", "--format", "{{.State.Status}}|{{.State.ExitCode}}|{{.State.OOMKilled}}", containerName], TimeSpan.FromSeconds(30));
        if (result.ExitCode != 0)
        {
            return null; // "No such object" — the container doesn't exist.
        }

        var parts = result.StdOut.Trim().Split('|');
        return parts.Length == 3 && int.TryParse(parts[1], out var exitCode)
            ? new ContainerState(parts[0], exitCode, string.Equals(parts[2], "true", StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static async Task RemoveContainerAsync(string containerName)
    {
        try
        {
            await RunDockerCommandAsync(["rm", "-f", containerName], TimeSpan.FromSeconds(30));
        }
        catch
        {
            // Best-effort — ScanContainerJanitorService sweeps leftovers.
        }
    }

    private static ProcessStartInfo DockerStartInfo(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
    }

    internal static async Task<(int ExitCode, string StdOut, string StdErr)> RunDockerCommandAsync(IReadOnlyList<string> args, TimeSpan timeout)
    {
        using var process = new Process { StartInfo = DockerStartInfo([.. args]) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already gone.
            }

            throw;
        }

        return (process.ExitCode, await stdout, await stderr);
    }

    // Removes the scan's container and reconftw output folder once its result is safely recorded.
    public async Task CleanupAsync(Scan scan)
    {
        await RemoveContainerAsync(ContainerNameFor(scan.Id));
        DeleteReconftwOutput(scan);
    }

    // Best-effort — if this fails for any reason (daemon hiccup, container already gone), the
    // fallback Kill(entireProcessTree) in the caller still guarantees the local CLI process
    // doesn't leak, even though the container itself might then linger until it finishes on its
    // own. `docker stop` gives it a 10s grace period (SIGTERM) before SIGKILL, matching the
    // Docker CLI default.
    private static async Task StopContainerAsync(string containerName)
    {
        try
        {
            using var stopProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            stopProcess.StartInfo.ArgumentList.Add("stop");
            stopProcess.StartInfo.ArgumentList.Add(containerName);
            stopProcess.Start();
            await stopProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch
        {
            // Swallowed intentionally — see method summary.
        }
    }

    // Tool command lines, built only from validated settings (ScanToolOptionsValidator) — never
    // from free text. The base flags are unchanged from the previously hardcoded invocations:
    //   Naabu  — -host wants a bare host/IP/CIDR (a full URL makes it report "no valid ipv4 or ipv6
    //            targets"); -json gives structured records for NaabuOutputParser.
    //   Nuclei — -t reads templates from the persistent nuclei-templates volume (-ud only controls
    //            where -update-templates writes); -irr adds request/response for real PoCs; -stats
    //            keeps the console alive on clean targets; -silent keeps stdout = JSONL results.
    //   Reconftw — -d wants a bare domain; -o puts result files on the shared output volume.
    private IReadOnlyList<string> BuildToolArguments(Scan scan, string target, ScanToolRuntimeSettings settings, int reconftwParallel = 1)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        switch (scan.Tool)
        {
            case ScanTool.Naabu:
            {
                var o = settings.Naabu ?? NaabuOptions.Default;
                var args = new List<string> { "-host", ExtractHost(target), "-json", "-silent" };
                args.AddRange(o.PortPreset switch
                {
                    "top-100" => ["-top-ports", "100"],
                    "full" => ["-p", "-"],
                    "custom" when !string.IsNullOrWhiteSpace(o.CustomPorts) => ["-p", o.CustomPorts],
                    _ => ["-top-ports", "1000"],
                });
                args.AddRange(["-rate", o.Rate.ToString(inv), "-scan-type", o.ScanType == "syn" ? "s" : "c"]);
                return args;
            }

            case ScanTool.Nuclei:
            {
                var o = settings.Nuclei ?? NucleiOptions.Default;
                var args = new List<string> { "-u", target, "-t", "/tmp/nuclei-templates", "-jsonl", "-irr", "-silent", "-stats", "-stats-interval", "5" };
                if (o.Severities.Count > 0) args.AddRange(["-severity", string.Join(',', o.Severities)]);
                if (o.IncludeTags.Count > 0) args.AddRange(["-tags", string.Join(',', o.IncludeTags)]);
                if (o.ExcludeTags.Count > 0) args.AddRange(["-etags", string.Join(',', o.ExcludeTags)]);
                args.AddRange(["-rate-limit", o.RateLimit.ToString(inv), "-c", o.Concurrency.ToString(inv)]);
                return args;
            }

            case ScanTool.Reconftw:
            {
                var o = (settings.Reconftw ?? ReconftwOptions.Default).Normalized();
                // -s covers subdomain discovery + web probe/screenshots/takeover; any other module
                // needs the full recon flow (-r), which still only runs what the config enables.
                var modeFlag = o.Modules!.All(ReconftwModules.SubdomainModeModules.Contains) ? "-s" : "-r";
                var configPath = WriteReconftwConfig(scan, o, reconftwParallel) ?? "/reconftw/reconftw.lean.cfg";
                return ["-d", ExtractHost(target), modeFlag, "-f", configPath, "-o", $"/scan-output/scan-{scan.Id}"];
            }

            case ScanTool.Custom:
            {
                // Admin-defined argv; placeholders are substituted per token (no shell, so the
                // target can never be interpreted as extra arguments or commands).
                var custom = settings.Custom ?? throw new InvalidOperationException("Custom scan without a custom tool snapshot.");
                var host = ExtractHost(target);
                var url = Uri.TryCreate(target, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
                    ? target
                    : $"https://{host}";
                var port = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Port.ToString(inv) : "443";
                return custom.Arguments
                    .Select(a => a.Replace("{target}", target).Replace("{host}", host).Replace("{url}", url).Replace("{port}", port))
                    .ToList();
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(scan), scan.Tool, "Unsupported scan tool.");
        }
    }

    private string ReconftwConfigFile(Scan scan) => Path.Combine(_settingsProvider.ReconftwOutputPath, $"scan-{scan.Id}.cfg");

    // Per-scan reconftw config on the shared output volume: sources a base config, then applies
    // the Scan Tools module checkboxes. Only booleans/integers produced here are written — no user
    // text (module keys are validated against ReconftwModules; flags come from that fixed list).
    // Returns the in-container path, or null when the volume isn't mounted (local dev), in which
    // case the baked lean config is used as-is.
    private string? WriteReconftwConfig(Scan scan, ReconftwOptions o, int parallel)
    {
        if (!Directory.Exists(_settingsProvider.ReconftwOutputPath))
        {
            return null;
        }

        var threads = o.Threads.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var selected = o.EffectiveModules().ToHashSet();
        var content = new StringBuilder();
        content.AppendLine($"# Generated by ShieldReport for scan {scan.Id} from the Scan Tools settings.");
        content.AppendLine("source /reconftw/reconftw.lean.cfg");
        // Every module switch is written explicitly (checked = true, unchecked = false), so the
        // result never depends on the base config's defaults. Active attacks stay off.
        foreach (var module in ReconftwModules.All)
        {
            foreach (var flag in module.ConfigFlags)
            {
                content.AppendLine($"{flag}={(selected.Contains(module.Key) ? "true" : "false")}");
            }
        }

        content.AppendLine("SUBDOMAINS_GENERAL=true");
        content.AppendLine("VULNS_GENERAL=false");
        content.AppendLine("PORTSCAN_PASSIVE=false");   // needs a Shodan key
        content.AppendLine($"HTTPX_THREADS={threads}");
        content.AppendLine($"HTTPX_UNCOMMONPORTS_THREADS={threads}");
        content.AppendLine($"RESOLVE_DOMAINS_THREADS={threads}");
        content.AppendLine($"DNSTAKE_THREADS={threads}");
        // This scan's share of the shared DNS budget (never above the lean config's per-scan caps).
        var publicLimit = Math.Clamp(_settingsProvider.ReconftwDnsBudget / parallel, 50, 500);
        var trustedLimit = Math.Clamp(_settingsProvider.ReconftwTrustedDnsBudget / parallel, 20, 200);
        content.AppendLine($"PUREDNS_PUBLIC_LIMIT={publicLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        content.AppendLine($"PUREDNS_TRUSTED_LIMIT={trustedLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        File.WriteAllText(ReconftwConfigFile(scan), content.ToString());
        return $"/scan-output/scan-{scan.Id}.cfg";
    }

    // Output-line callbacks run inside async-void Process event handlers: an exception escaping
    // one (e.g. a SignalR push failing during shutdown) would be unhandled on the thread pool and
    // terminate the whole API process. Live streaming is best-effort, so swallow and move on.
    private static async Task SafeOnOutputLineAsync(Func<string, string, Task> onOutputLine, string line, string stream)
    {
        try
        {
            await onOutputLine(line, stream);
        }
        catch
        {
            // Intentionally ignored — see method comment.
        }
    }

    // Trims from the front once the builder grows 25% past the cap, so the trim cost is amortized
    // instead of paid on every line.
    private static void KeepTail(StringBuilder builder, int capChars)
    {
        if (builder.Length > capChars + capChars / 4)
        {
            builder.Remove(0, builder.Length - capChars);
        }
    }

    private string ReconftwScanOutputDirectory(Scan scan) => Path.Combine(_settingsProvider.ReconftwOutputPath, $"scan-{scan.Id}");

    // Returns the de-duplicated contents of every subdomains/subdomains.txt reconftw produced for
    // this scan (one hostname per line, the shape ReconftwOutputParser already parses), or null
    // when the output volume isn't mounted here (e.g. the API running outside docker compose) so
    // the caller falls back to parsing stdout. The folder is removed later by CleanupAsync.
    private string? ReadReconftwResults(Scan scan)
    {
        var directory = ReconftwScanOutputDirectory(scan);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            var hosts = Directory.EnumerateFiles(directory, "subdomains.txt", SearchOption.AllDirectories)
                .Where(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "subdomains", StringComparison.Ordinal))
                .SelectMany(File.ReadLines)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return string.Join('\n', hosts);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void DeleteReconftwOutput(Scan scan)
    {
        if (scan.Tool != ScanTool.Reconftw)
        {
            return;
        }

        try
        {
            var directory = ReconftwScanOutputDirectory(scan);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            var configFile = ReconftwConfigFile(scan);
            if (File.Exists(configFile))
            {
                File.Delete(configFile);
            }
        }
        catch
        {
            // Best-effort cleanup — a leftover folder is not worth failing the scan over.
        }
    }

    // Strips scheme/path/query from a WebUrl-shaped identifier so host-only tools get just the
    // hostname or IP; identifiers that are already bare (IpRange/other asset types) pass through.
    private static string ExtractHost(string target)
    {
        return Uri.TryCreate(target, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host
            : target.TrimEnd('/');
    }
}
