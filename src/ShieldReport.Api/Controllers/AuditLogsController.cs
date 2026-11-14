using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

// Read-only audit trail: data changes (AuditSaveChangesInterceptor) and security events
// (IAuditLogger: sign-ins, MFA, downloads). Newest first.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit-logs")]
[Authorize(Policy = Permissions.AuditLogsRead)]
public sealed class AuditLogsController : ControllerBase
{
    private const int MaxPageSize = 200;

    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogsController(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogRow>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] long? userId = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = await _auditLogRepository.SearchAsync(
            new AuditLogQuery(page, pageSize, userId, entityName, entityId, action, search, from?.ToUniversalTime(), to?.ToUniversalTime()),
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AuditLogRow>>.SuccessResponse(PagedResult<AuditLogRow>.Create(items, total, page, pageSize), "Audit logs retrieved successfully"));
    }

    // Entity types present in the log, for the UI's filter dropdown.
    [HttpGet("entity-names")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EntityNames(CancellationToken cancellationToken)
    {
        var names = await _auditLogRepository.GetDistinctEntityNamesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.SuccessResponse(names, "Entity names retrieved successfully"));
    }
}
