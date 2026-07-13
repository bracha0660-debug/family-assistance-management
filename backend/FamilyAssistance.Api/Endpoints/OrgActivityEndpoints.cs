using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class OrgActivityEndpoints
{
    public static void MapOrgActivityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/activity", ListActivity).RequireOrgAdmin();
    }

    private static async Task<IResult> ListActivity(
        int? limit,
        int? offset,
        HttpContext httpContext,
        OrganizationActivityService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ListActivityAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, limit, offset, cancellationToken);
        if (!result.IsSuccess)
        {
            return Results.Json(
                new ApiError { Error = result.Error, Code = result.Code, Details = result.Details },
                statusCode: result.StatusCode);
        }

        return Results.Ok(result.Value);
    }
}
