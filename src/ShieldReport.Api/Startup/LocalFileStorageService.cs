using ShieldReport.Application.Common.Interfaces.Services;

namespace ShieldReport.Api.Startup;

public sealed class LocalFileStorageService : IFileStorageService
{
    private const string UploadsFolderName = "App_Data/uploads";

    private readonly IWebHostEnvironment _environment;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsync(string subPath, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var relativeDirectory = Path.Combine(UploadsFolderName, subPath);
        var absoluteDirectory = Path.Combine(_environment.ContentRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, safeFileName);
        using var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        return Path.Combine(relativeDirectory, safeFileName).Replace(Path.DirectorySeparatorChar, '/');
    }

    public void Delete(string relativeUrl)
    {
        var absolutePath = Path.Combine(_environment.ContentRootPath, relativeUrl.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }
}
