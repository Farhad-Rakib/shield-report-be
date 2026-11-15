using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.LlmProviderCatalog;

public sealed record LlmProviderCatalogItemDto(long Id, string Name, string? Model, DateTime CreatedAt);

// Model is optional.
public sealed record SaveLlmProviderCatalogItemRequestDto(string Name, string? Model);

public interface ILlmProviderCatalogService
{
    Task<IReadOnlyList<LlmProviderCatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<LlmProviderCatalogItemDto> CreateAsync(SaveLlmProviderCatalogItemRequestDto request, CancellationToken cancellationToken = default);
    Task<LlmProviderCatalogItemDto> UpdateAsync(long id, SaveLlmProviderCatalogItemRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

// AI -> Provider List: a plain lookup (name + optional model) offered by the Providers & Models
// form. Nothing else depends on these rows, so delete is a hard delete.
public sealed class LlmProviderCatalogService : ILlmProviderCatalogService
{
    private const int MaxNameLength = 100;
    private const int MaxModelLength = 200;

    private readonly IRepository<LlmProviderCatalogItem> _items;
    private readonly IUnitOfWork _unitOfWork;

    public LlmProviderCatalogService(IRepository<LlmProviderCatalogItem> items, IUnitOfWork unitOfWork)
    {
        _items = items;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<LlmProviderCatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await _items.GetAllAsync(cancellationToken))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToList();

    public async Task<LlmProviderCatalogItemDto> CreateAsync(SaveLlmProviderCatalogItemRequestDto request, CancellationToken cancellationToken = default)
    {
        var (name, model) = Validate(request);
        await EnsureUniqueNameAsync(name, null, cancellationToken);
        var item = await _items.AddAsync(new LlmProviderCatalogItem(name, model), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<LlmProviderCatalogItemDto> UpdateAsync(long id, SaveLlmProviderCatalogItemRequestDto request, CancellationToken cancellationToken = default)
    {
        var item = await _items.GetByIdAsync(id, cancellationToken) ?? throw new AppException("Provider not found.", 404);
        var (name, model) = Validate(request);
        await EnsureUniqueNameAsync(name, id, cancellationToken);
        item.Update(name, model);
        item.UpdatedAt = DateTime.UtcNow;
        _items.Update(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _items.GetByIdAsync(id, cancellationToken) ?? throw new AppException("Provider not found.", 404);
        _items.Delete(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static (string Name, string? Model) Validate(SaveLlmProviderCatalogItemRequestDto request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim();
        if (name.Length == 0)
        {
            throw new AppException("Name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new AppException($"Name must be at most {MaxNameLength} characters.");
        }

        if (model is { Length: > MaxModelLength })
        {
            throw new AppException($"Model must be at most {MaxModelLength} characters.");
        }

        return (name, model);
    }

    private async Task EnsureUniqueNameAsync(string name, long? exceptId, CancellationToken cancellationToken)
    {
        var all = await _items.GetAllAsync(cancellationToken);
        if (all.Any(x => x.Id != exceptId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppException($"A provider named \"{name}\" already exists.", 409);
        }
    }

    private static LlmProviderCatalogItemDto ToDto(LlmProviderCatalogItem x) => new(x.Id, x.Name, x.Model, x.CreatedAt);
}
