using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.LlmPrompts;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

// Prompt Library: the prompts sent to the active LLM provider, with per-key overrides.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/llm-prompts")]
[Authorize]
public sealed class LlmPromptsController : ControllerBase
{
    private readonly ILlmPromptService _service;
    private readonly ICurrentUserService _currentUserService;

    public LlmPromptsController(ILlmPromptService service, ICurrentUserService currentUserService)
    {
        _service = service;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsRead)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<LlmPromptDto>>.SuccessResponse(await _service.GetAllAsync(cancellationToken), "Prompts retrieved successfully"));

    [HttpPut("{key}")]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsUpdate)]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateLlmPromptRequestDto request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LlmPromptDto>.SuccessResponse(await _service.UpdateAsync(key, request, _currentUserService.UserId, cancellationToken), "Prompt saved"));

    [HttpPost("{key}/reset")]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsUpdate)]
    public async Task<IActionResult> Reset(string key, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LlmPromptDto>.SuccessResponse(await _service.ResetAsync(key, cancellationToken), "Prompt reset to the default"));

    // Renders the prompt with sample values and sends it to the active provider (saved template, or
    // the unsaved one in the body). Provider failures come back as Success=false.
    [HttpPost("{key}/test")]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsUpdate)]
    public async Task<IActionResult> Test(string key, [FromBody] TestLlmPromptRequestDto? request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LlmPromptTestResultDto>.SuccessResponse(await _service.TestAsync(key, request ?? new TestLlmPromptRequestDto(null), cancellationToken), "Prompt test finished"));
}
