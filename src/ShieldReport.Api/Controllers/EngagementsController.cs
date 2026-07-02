using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Engagements;
using ShieldReport.Application.Engagements.Dtos;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/engagements")]
[Authorize]
public sealed class EngagementsController : ControllerBase
{
    private readonly IEngagementService _engagementService;
    private readonly IEngagementTaskService _engagementTaskService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateEngagementRequestDto> _createValidator;
    private readonly IValidator<UpdateEngagementRequestDto> _updateValidator;
    private readonly IValidator<AssignEngagementUserRequestDto> _assignValidator;
    private readonly IValidator<CreateEngagementTaskRequestDto> _createTaskValidator;
    private readonly IValidator<UpdateEngagementTaskRequestDto> _updateTaskValidator;

    public EngagementsController(
        IEngagementService engagementService,
        IEngagementTaskService engagementTaskService,
        ICurrentUserService currentUserService,
        IValidator<CreateEngagementRequestDto> createValidator,
        IValidator<UpdateEngagementRequestDto> updateValidator,
        IValidator<AssignEngagementUserRequestDto> assignValidator,
        IValidator<CreateEngagementTaskRequestDto> createTaskValidator,
        IValidator<UpdateEngagementTaskRequestDto> updateTaskValidator)
    {
        _engagementService = engagementService;
        _engagementTaskService = engagementTaskService;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _assignValidator = assignValidator;
        _createTaskValidator = createTaskValidator;
        _updateTaskValidator = updateTaskValidator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.EngagementsRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<EngagementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] long? clientOrganizationId, CancellationToken cancellationToken)
    {
        var engagements = await _engagementService.ListEngagementsAsync(request, clientOrganizationId, cancellationToken);
        return Ok(ApiResponse<PagedResult<EngagementDto>>.SuccessResponse(engagements, "Engagements retrieved successfully"));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Permissions.EngagementsRead)]
    [ProducesResponseType(typeof(ApiResponse<EngagementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var engagement = await _engagementService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<EngagementDto>.SuccessResponse(engagement, "Engagement retrieved successfully"));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.EngagementsCreate)]
    [ProducesResponseType(typeof(ApiResponse<EngagementDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEngagementRequestDto request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var createdByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);

        var engagement = await _engagementService.CreateAsync(request, createdByUserId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = engagement.Id }, ApiResponse<EngagementDto>.SuccessResponse(engagement, "Engagement created successfully", StatusCodes.Status201Created));
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = Permissions.EngagementsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<EngagementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateEngagementRequestDto request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var engagement = await _engagementService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<EngagementDto>.SuccessResponse(engagement, "Engagement updated successfully"));
    }

    [HttpPost("{id:long}/assignees")]
    [Authorize(Policy = Permissions.EngagementsAssign)]
    [ProducesResponseType(typeof(ApiResponse<EngagementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignUser(long id, [FromBody] AssignEngagementUserRequestDto request, CancellationToken cancellationToken)
    {
        await _assignValidator.ValidateAndThrowAsync(request, cancellationToken);
        var engagement = await _engagementService.AssignUserAsync(id, request, cancellationToken);
        return Ok(ApiResponse<EngagementDto>.SuccessResponse(engagement, "User assigned successfully"));
    }

    [HttpDelete("{id:long}/assignees/{userId:long}")]
    [Authorize(Policy = Permissions.EngagementsAssign)]
    [ProducesResponseType(typeof(ApiResponse<EngagementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAssignee(long id, long userId, CancellationToken cancellationToken)
    {
        var engagement = await _engagementService.RemoveAssigneeAsync(id, userId, cancellationToken);
        return Ok(ApiResponse<EngagementDto>.SuccessResponse(engagement, "Assignee removed successfully"));
    }

    [HttpGet("{id:long}/tasks")]
    [Authorize(Policy = Permissions.EngagementsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EngagementTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTasks(long id, CancellationToken cancellationToken)
    {
        var tasks = await _engagementTaskService.ListByEngagementAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EngagementTaskDto>>.SuccessResponse(tasks, "Engagement tasks retrieved successfully"));
    }

    [HttpPost("{id:long}/tasks")]
    [Authorize(Policy = Permissions.EngagementsAssign)]
    [ProducesResponseType(typeof(ApiResponse<EngagementTaskDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTask(long id, [FromBody] CreateEngagementTaskRequestDto request, CancellationToken cancellationToken)
    {
        await _createTaskValidator.ValidateAndThrowAsync(request, cancellationToken);
        var createdByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);

        var task = await _engagementTaskService.CreateAsync(id, request, createdByUserId, cancellationToken);
        return CreatedAtAction(nameof(ListTasks), new { id }, ApiResponse<EngagementTaskDto>.SuccessResponse(task, "Engagement task created successfully", StatusCodes.Status201Created));
    }

    [HttpPatch("{id:long}/tasks/{taskId:long}")]
    [Authorize(Policy = Permissions.EngagementsAssign)]
    [ProducesResponseType(typeof(ApiResponse<EngagementTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTask(long id, long taskId, [FromBody] UpdateEngagementTaskRequestDto request, CancellationToken cancellationToken)
    {
        await _updateTaskValidator.ValidateAndThrowAsync(request, cancellationToken);
        var task = await _engagementTaskService.UpdateAsync(id, taskId, request, cancellationToken);
        return Ok(ApiResponse<EngagementTaskDto>.SuccessResponse(task, "Engagement task updated successfully"));
    }

    [HttpPatch("{id:long}/tasks/{taskId:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<EngagementTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTaskStatus(long id, long taskId, [FromBody] UpdateEngagementTaskStatusRequestDto request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var hasAssignPermission = User.IsInRole(ShieldReport.Domain.Enums.SystemRoles.SuperAdmin)
            || User.Claims.Any(c => c.Type == "permission" && c.Value == Permissions.EngagementsAssign);

        var task = await _engagementTaskService.UpdateStatusAsync(id, taskId, request, currentUserId, hasAssignPermission, cancellationToken);
        return Ok(ApiResponse<EngagementTaskDto>.SuccessResponse(task, "Engagement task status updated successfully"));
    }
}
