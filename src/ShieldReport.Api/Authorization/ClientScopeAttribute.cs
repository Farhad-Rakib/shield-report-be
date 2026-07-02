using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Interfaces;

namespace ShieldReport.Api.Authorization;

// Defense-in-depth, not the primary isolation layer (that's the EF Core HasQueryFilter).
// Mismatch returns 404, never 403 — a client probing another org's id can't even confirm
// it exists (see BUSINESS-FLOW-PentestOps.md FAQ). PLATFORM_ADMIN/PENTESTER bypass entirely.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class ClientScopeAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _routeParameterName;

    public ClientScopeAttribute(string routeParameterName = "clientId")
    {
        _routeParameterName = routeParameterName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var currentUserService = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

        if (!currentUserService.IsClientPortalUser)
        {
            await next();
            return;
        }

        var routeValue = ResolveRouteValue(context);
        if (routeValue is null || !long.TryParse(routeValue, out var requestedClientOrganizationId)
            || requestedClientOrganizationId != currentUserService.ClientOrganizationId)
        {
            context.Result = new NotFoundObjectResult(ApiResponse.FailureResponse("Resource not found.", StatusCodes.Status404NotFound));
            return;
        }

        await next();
    }

    private string? ResolveRouteValue(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue(_routeParameterName, out var boundValue) && boundValue is not null)
        {
            return boundValue.ToString();
        }

        return context.RouteData.Values.TryGetValue(_routeParameterName, out var rawValue)
            ? rawValue?.ToString()
            : null;
    }
}
