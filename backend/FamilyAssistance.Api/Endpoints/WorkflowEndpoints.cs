using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");
        group.MapGet("/workflow/dashboard", GetDashboard).RequireOrgContext();
    }

    private static async Task<IResult> GetDashboard(
        HttpContext httpContext,
        WorkflowDashboardService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var canView = auth.FullOrgAccess
            || auth.HasGrant(PermissionKeys.CommitteeDecisionsView)
            || auth.HasGrant(PermissionKeys.PaymentsView)
            || auth.HasGrant(PermissionKeys.FamiliesView);

        if (!canView)
        {
            return Results.Json(
                new ApiError { Error = "אין הרשאה", Code = "FORBIDDEN" },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await service.GetDashboardAsync(
            auth.EffectiveOrganizationId!.Value, auth, cancellationToken);
        return Results.Ok(result);
    }
}
