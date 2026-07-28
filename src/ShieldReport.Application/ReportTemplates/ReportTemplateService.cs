using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.ReportTemplates.Dtos;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.ReportTemplates;

public sealed class ReportTemplateService : IReportTemplateService
{
    private readonly IReportTemplateRepository _repository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public ReportTemplateService(IReportTemplateRepository repository, IFileStorageService fileStorageService, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ReportTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _repository.GetAllAsync(cancellationToken);
        return templates.Select(MapToDto).ToList();
    }

    public async Task<ReportTemplateDto> CreateAsync(CreateReportTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        var template = new ReportTemplate(request.Name, request.CompanyDisplayName);
        await _repository.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task<ReportTemplateDto> UpdateAsync(long id, UpdateReportTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        template.UpdateDetails(request.Name, request.CompanyDisplayName);
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task<ReportTemplateDto> UploadDocxTemplateAsync(long id, Stream fileContent, string fileName, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        if (!fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("Only .docx files are supported.", 400);
        }

        // Cheap structural sanity check — every .docx is a ZIP archive ("PK" magic bytes) — rather
        // than trusting the client-supplied content-type/extension alone.
        var header = new byte[2];
        var read = await fileContent.ReadAsync(header, 0, 2, cancellationToken);
        if (read < 2 || header[0] != 0x50 || header[1] != 0x4B)
        {
            throw new AppException("The uploaded file is not a valid .docx document.", 400);
        }
        fileContent.Position = 0;

        var relativePath = await _fileStorageService.SaveAsync($"report-templates/{template.Id}", "template.docx", fileContent, cancellationToken);
        template.SetDocxTemplate(relativePath, fileName);
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task<ReportTemplateDto> ClearDocxTemplateAsync(long id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        if (template.DocxTemplatePath is not null)
        {
            _fileStorageService.Delete(template.DocxTemplatePath);
        }

        template.ClearDocxTemplate();
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task<ReportTemplateDto> SetDefaultAsync(long id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        if (!template.IsDefault)
        {
            var currentDefault = await _repository.GetDefaultTrackedAsync(cancellationToken);
            currentDefault?.UnmarkAsDefault();
            if (currentDefault is not null)
            {
                _repository.Update(currentDefault);
            }

            template.MarkAsDefault();
            _repository.Update(template);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return;
        }

        _repository.Delete(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static ReportTemplateDto MapToDto(ReportTemplate template)
        => new(template.Id, template.Name, template.CompanyDisplayName, template.IsDefault, template.DocxTemplatePath is not null, template.DocxTemplateFileName);
}
