using ShieldReport.Application.ScanTools;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Runners;

// "Test" on the Scan Tools page: runs a harmless version/help command in the configured image to
// confirm it is present locally and starts. Never touches a target.
public sealed class DockerScanToolDiagnostics : IScanToolDiagnostics
{
    public async Task<ScanToolTestResultDto> TestAsync(ScanTool tool, string image, CancellationToken cancellationToken = default)
    {
        string[] args = tool switch
        {
            ScanTool.Naabu => ["run", "--rm", "--user", "1000:1000", "--env", "HOME=/tmp", image, "-version"],
            ScanTool.Nuclei => ["run", "--rm", "--user", "1000:1000", "--env", "HOME=/tmp", image, "-version"],
            ScanTool.Reconftw => ["run", "--rm", "--platform", "linux/arm64", "--entrypoint", "bash", image, "-c",
                "cd /reconftw && git describe --tags --always 2>/dev/null || echo reconftw present; test -f /reconftw/reconftw.lean.cfg && echo lean config: present"],
            _ => throw new ArgumentOutOfRangeException(nameof(tool))
        };

        try
        {
            // Pulling is not allowed here (--pull=never semantics via an inspect first): a missing
            // image should be reported, not silently downloaded from a registry.
            var inspect = await DockerScanRunner.RunDockerCommandAsync(["image", "inspect", "--format", "{{.Id}}", image], TimeSpan.FromSeconds(30));
            if (inspect.ExitCode != 0)
            {
                return new ScanToolTestResultDto(false, image, "Image is not present on the Docker host. Run `docker compose up` (or pull it) first.");
            }

            var run = await DockerScanRunner.RunDockerCommandAsync(args, TimeSpan.FromSeconds(90));
            var output = string.Join('\n', new[] { run.StdOut, run.StdErr }.Where(o => !string.IsNullOrWhiteSpace(o))).Trim();
            output = System.Text.RegularExpressions.Regex.Replace(output, @"\x1B\[[0-9;]*m", string.Empty);
            if (output.Length > 2000) output = output[..2000];
            return new ScanToolTestResultDto(run.ExitCode == 0, image, output.Length > 0 ? output : $"exit code {run.ExitCode}");
        }
        catch (Exception ex)
        {
            return new ScanToolTestResultDto(false, image, $"Docker is not reachable: {ex.Message}");
        }
    }

    public async Task<ScanToolTestResultDto> TestImageAsync(string image, CancellationToken cancellationToken = default)
    {
        try
        {
            var inspect = await DockerScanRunner.RunDockerCommandAsync(["image", "inspect", "--format", "{{.Id}} {{.Os}}/{{.Architecture}} {{.Size}}", image], TimeSpan.FromSeconds(30));
            var pulled = false;
            if (inspect.ExitCode != 0)
            {
                // Custom tools are pulled on first test so the first real scan doesn't stall on it.
                var pull = await DockerScanRunner.RunDockerCommandAsync(["pull", image], TimeSpan.FromMinutes(5));
                if (pull.ExitCode != 0)
                {
                    var error = (pull.StdErr + pull.StdOut).Trim();
                    return new ScanToolTestResultDto(false, image, $"Could not pull the image: {(error.Length > 1500 ? error[..1500] : error)}");
                }

                pulled = true;
                inspect = await DockerScanRunner.RunDockerCommandAsync(["image", "inspect", "--format", "{{.Id}} {{.Os}}/{{.Architecture}} {{.Size}}", image], TimeSpan.FromSeconds(30));
            }

            var parts = inspect.StdOut.Trim().Split(' ');
            var sizeMb = parts.Length > 2 && long.TryParse(parts[2], out var bytes) ? $"{bytes / 1024 / 1024} MB" : "?";
            var id = parts[0].Length > 19 ? parts[0][..19] : parts[0];
            return new ScanToolTestResultDto(inspect.ExitCode == 0, image,
                $"{(pulled ? "Pulled. " : "Present. ")}Image {id}, {(parts.Length > 1 ? parts[1] : "?")}, {sizeMb}.");
        }
        catch (Exception ex)
        {
            return new ScanToolTestResultDto(false, image, $"Docker is not reachable: {ex.Message}");
        }
    }
}
