using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class OrgUsersEndpoints
{
    public static void MapOrgUsersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/users", ListUsers).RequireOrgAdmin();
        group.MapPost("/users", CreateUser).RequireOrgAdmin();
        group.MapPatch("/users/{id:guid}", UpdateUser).RequireOrgAdmin();
        group.MapPatch("/users/{id:guid}/disable", DisableUser).RequireOrgAdmin();
    }

    private static async Task<IResult> ListUsers(
        HttpContext httpContext,
        OrganizationUserService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ListUsersAsync(
            currentUser.OrganizationId!.Value, currentUser.UserId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateUser(
        CreateOrgUserRequest request,
        HttpContext httpContext,
        OrganizationUserService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.CreateUserAsync(
            currentUser.OrganizationId!.Value, request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Json(
            new OrgUserResponse { User = result.Value! },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateUser(
        Guid id,
        UpdateOrgUserRequest request,
        HttpContext httpContext,
        OrganizationUserService service,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch(httpContext);
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.UpdateUserAsync(
            currentUser.OrganizationId!.Value, id, request, version, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new OrgUserResponse { User = result.Value! });
    }

    private static async Task<IResult> DisableUser(
        Guid id,
        DisableOrgUserRequest request,
        HttpContext httpContext,
        OrganizationUserService service,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch(httpContext);
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.DisableUserAsync(
            currentUser.OrganizationId!.Value, id, request, version, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new OrgUserResponse { User = result.Value! });
    }

    private static int? ReadIfMatch(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("If-Match", out var ifMatch) &&
            int.TryParse(ifMatch.ToString(), out var parsed))
        {
            return parsed;
        }
        return null;
    }

    private static IResult ToError<T>(ServiceResult<T> result) =>
        Results.Json(
            new ApiError
            {
                Error = result.Error,
                Code = result.Code,
                Details = result.Details
            },
            statusCode: result.StatusCode);
}
