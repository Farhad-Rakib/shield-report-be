namespace ShieldReport.Application.Common.Interfaces.Services;

// Local filesystem only — no blob SDK in use (matches the existing storage convention for
// raw scan logs/local paths elsewhere in this codebase). Implemented in Api since it needs
// IWebHostEnvironment to resolve a content-root-relative path.
public interface IFileStorageService
{
    Task<string> SaveAsync(string subPath, string fileName, Stream content, CancellationToken cancellationToken = default);

    void Delete(string relativeUrl);
}
