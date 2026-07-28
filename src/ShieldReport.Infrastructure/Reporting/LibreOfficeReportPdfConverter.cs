using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using ShieldReport.Application.Common.Interfaces.Services;

namespace ShieldReport.Infrastructure.Reporting;

// Shells out to LibreOffice headless — same external-process invocation pattern as
// DockerScanRunner (ProcessStartInfo + ArgumentList, never a shelled string). There is no
// docx->PDF library in .NET without a commercial SDK, and QuestPDF only renders new PDFs from
// scratch — it can't convert an existing, hand-filled Word document.
public sealed class LibreOfficeReportPdfConverter : IReportPdfConverter
{
    private readonly string _sofficePath;

    public LibreOfficeReportPdfConverter(IConfiguration configuration)
    {
        _sofficePath = configuration["ReportConversion:SofficePath"]
            ?? (OperatingSystem.IsMacOS() && File.Exists("/Applications/LibreOffice.app/Contents/MacOS/soffice")
                ? "/Applications/LibreOffice.app/Contents/MacOS/soffice"
                : "soffice");
    }

    public async Task<byte[]> ConvertAsync(byte[] docxBytes, CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"shieldreport-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            var inputPath = Path.Combine(workDir, "report.docx");
            await File.WriteAllBytesAsync(inputPath, docxBytes, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = _sofficePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                // Isolates this conversion's LibreOffice profile from any concurrent conversion
                // and from a user's own running LibreOffice instance, which otherwise makes
                // --headless silently fail to produce output.
                WorkingDirectory = workDir
            };
            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add("-env:UserInstallation=file://" + workDir.Replace('\\', '/') + "/lo-profile");
            startInfo.ArgumentList.Add("--convert-to");
            startInfo.ArgumentList.Add("pdf");
            startInfo.ArgumentList.Add("--outdir");
            startInfo.ArgumentList.Add(workDir);
            startInfo.ArgumentList.Add(inputPath);

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdErr = await stdErrTask;

            var outputPath = Path.Combine(workDir, "report.pdf");
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                throw new InvalidOperationException($"soffice conversion failed (exit {process.ExitCode}): {stdErr}");
            }

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup — a leaked temp dir here isn't worth failing the request over.
            }
        }
    }
}
