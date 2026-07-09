using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Runners;

public sealed partial class DockerScanRunner : IScanRunner
{
    // Strict allowlist for a URL/hostname/IP/CIDR target — alnum plus the handful of
    // characters those shapes legitimately need. Excludes every shell metacharacter
    // (;, &, |, `, $, etc.) by construction, not just by a denylist check.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.\-:/_]{0,253}$")]
    private static partial Regex TargetAllowlistRegex();

    private static readonly char[] ShellMetacharacters = [';', '&', '|', '`', '$', '\n', '\r', '<', '>'];

    public async Task<ScanRunResult> RunAsync(Scan scan, ClientAsset asset, Func<string, Task> onOutputLine, CancellationToken cancellationToken = default)
    {
        var target = asset.Identifier;
        if (!TargetAllowlistRegex().IsMatch(target) || target.IndexOfAny(ShellMetacharacters) >= 0)
        {
            return new ScanRunResult(false, string.Empty, $"Target '{target}' failed the allowlist validation — refusing to invoke the scan container.");
        }

        var (image, toolArgs) = BuildToolInvocation(scan.Tool, target);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--rm");
        // six2dez/reconftw:latest owns /reconftw as root and its script does git operations
        // there — running as UID 1000 trips git's "dubious ownership" check and then fails to
        // mkdir its own output dir (Permission denied), confirmed against the real image.
        // Naabu/Nuclei have no such requirement, so only Reconftw runs as the image's default
        // (root) user; the container is still ephemeral (--rm) and locked down via no-new-privileges.
        if (scan.Tool != ScanTool.Reconftw)
        {
            startInfo.ArgumentList.Add("--user");
            startInfo.ArgumentList.Add("1000:1000");
        }
        else
        {
            // six2dez/reconftw:latest only publishes a linux/arm64 manifest on Docker Hub (no
            // linux/amd64 entry at all — confirmed via `docker manifest inspect`), so without
            // this flag `docker run` fails outright on amd64 hosts with "no matching manifest
            // for linux/amd64". Forcing arm64 runs natively on arm64 hosts and via QEMU
            // emulation on amd64 hosts. Matches the platform pin on reconftw-image in
            // docker-compose.yml.
            startInfo.ArgumentList.Add("--platform");
            startInfo.ArgumentList.Add("linux/arm64");
        }
        startInfo.ArgumentList.Add("--security-opt");
        startInfo.ArgumentList.Add("no-new-privileges");
        // UID 1000 has no /etc/passwd entry in these images, so $HOME defaults to "/" and
        // tools that write a config/cache dir (e.g. nuclei) fail with a permission error —
        // confirmed against the real image. /tmp is world-writable regardless of UID.
        startInfo.ArgumentList.Add("--env");
        startInfo.ArgumentList.Add("HOME=/tmp");
        startInfo.ArgumentList.Add(image);
        foreach (var arg in toolArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var rawOutput = new StringBuilder();
        var errorOutput = new StringBuilder();

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += async (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                rawOutput.AppendLine(e.Data);
                await onOutputLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    errorOutput.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                return new ScanRunResult(false, rawOutput.ToString(), errorOutput.Length > 0 ? errorOutput.ToString() : $"docker exited with code {process.ExitCode}.");
            }

            return new ScanRunResult(true, rawOutput.ToString(), null);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ScanRunResult(false, rawOutput.ToString(), $"Failed to invoke docker: {ex.Message}");
        }
    }

    private static (string Image, string[] Args) BuildToolInvocation(ScanTool tool, string target)
    {
        return tool switch
        {
            // Naabu's -host wants a bare hostname/IP/CIDR — a WebUrl asset's full
            // "https://host/path" identifier makes it report "no valid ipv4 or ipv6 targets
            // found" and exit immediately, confirmed against a live run. -json switches from the
            // plain "host:port" text line to a structured record (host/ip/port/protocol/tls) so
            // NaabuOutputParser can report more than just the bare port number.
            ScanTool.Naabu => ("projectdiscovery/naabu:latest", new[] { "-host", ExtractHost(target), "-json", "-silent" }),
            // Nuclei's -u accepts either a full URL or a bare host, so the raw identifier is fine
            // as-is. -irr (include request/response) adds curl-command/request/response to each
            // JSONL record so NucleiOutputParser can populate a real proof-of-concept instead of
            // just a title.
            ScanTool.Nuclei => ("projectdiscovery/nuclei:latest", new[] { "-u", target, "-jsonl", "-irr", "-silent" }),
            // Reconftw's -d wants a bare domain, same problem as Naabu.
            ScanTool.Reconftw => ("six2dez/reconftw:latest", new[] { "-d", ExtractHost(target), "--recon" }),
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unsupported scan tool.")
        };
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
