using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
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
        group.MapPatch("/users/{id:guid}/restore", RestoreUser).RequireOrgAdmin();
        group.MapPost("/users/{id:guid}/reset-password", ResetPassword).RequireOrgAdmin();
        group.MapGet("/users/{id:guid}/permission-overrides", GetPermissionOverrides).RequireOrgAdmin();
        group.MapPut("/users/{id:guid}/permission-overrides", PutPermissionOverrides).RequireOrgAdmin();
        group.MapDelete("/users/{id:guid}/permission-overrides/{permissionKey}", DeletePermissionOverride).RequireOrgAdmin();
    }

    private static async Task<IResult> ListUsers(
        HttpContext httpContext,
        OrganizationUserService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ListUsersAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, currentUser.UserId, cancellationToken);
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
            currentUser.GetEffectiveOrganizationId()!.Value, request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Json(new OrgUserResponse { User = result.Value! }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateUser(
        Guid id,
        UpdateOrgUserRequest request,
        HttpContext httpContext,
        OrganizationUserService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.UpdateUserAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, id, request, ReadIfMatch(httpContext),
            currentUser.UserId, cancellationToken);
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
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.DisableUserAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, id, request, ReadIfMatch(httpContext),
            currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new OrgUserResponse { User = result.Value! });
    }

    private static async Task<IResult> RestoreUser(
        Guid id,
        RestoreOrgUserRequest request,
        HttpContext httpContext,
        OrganizationUserService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.RestoreUserAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, id, request, ReadIfMatch(httpContext),
            currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new OrgUserResponse { User = result.Value! });
    }

    private static async Task<IResult> ResetPassword(
        Guid id,
        ResetOrgUserPasswordRequest request,
        HttpContext httpContext,
        OrganizationUserService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ResetPasswordAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, id, request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new OrgUserResponse { User = result.Value! });
    }

    private static async Task<IResult> GetPermissionOverrides(
        Guid id,
        HttpContext httpContext,
        UserPermissionOverrideService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.GetAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, id, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> PutPermissionOverrides(
        Guid id,
        UpdateUserPermissionOverridesRequest request,
        HttpContext httpContext,
        UserPermissionOverrideService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ReplaceAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, id, request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> DeletePermissionOverride(
        Guid id,
        string permissionKey,
        HttpContext httpContext,
        UserPermissionOverrideService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.DeleteOneAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, id, permissionKey, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(result.Value);
    }

    private static int? ReadIfMatch(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("If-Match", out var ifMatch) &&
            int.TryParse(ifMatch.ToString(), out var parsed))
            return parsed;
        return null;
    }

    private static IResult ToError<T>(ServiceResult<T> result) =>
        Results.Json(
            new ApiError { Error = result.Error, Code = result.Code, Details = result.Details },
            statusCode: result.StatusCode);
}
