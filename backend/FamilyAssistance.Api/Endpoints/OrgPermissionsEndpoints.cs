using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class OrgPermissionsEndpoints
{
    public static void MapOrgPermissionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/permissions/catalog", GetCatalog).RequireOrgAdmin();
    }

    private static async Task<IResult> GetCatalog(
        OrganizationPermissionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetCatalogAsync(cancellationToken);
        return Results.Ok(result);
    }
}

public static class OrgRolesEndpoints
{
    public static void MapOrgRolesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/roles", ListRoles).RequireOrgAdmin();
        group.MapPost("/roles", CreateRole).RequireOrgAdmin();
        group.MapGet("/roles/{roleId:guid}", GetRole).RequireOrgAdmin();
        group.MapPatch("/roles/{roleId:guid}", UpdateRole).RequireOrgAdmin();
        group.MapPatch("/roles/{roleId:guid}/disable", DisableRole).RequireOrgAdmin();
        group.MapPatch("/roles/{roleId:guid}/restore", RestoreRole).RequireOrgAdmin();
        group.MapPut("/roles/{roleId:guid}/grants", UpdateGrants).RequireOrgAdmin();
        group.MapPost("/roles/{roleId:guid}/grants/reset", ResetGrants).RequireOrgAdmin();
    }

    private static async Task<IResult> ListRoles(
        HttpContext httpContext,
        OrganizationRoleService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var roles = await service.ListRolesAsync(currentUser.GetEffectiveOrganizationId()!.Value, cancellationToken);
        return Results.Ok(new OrganizationRoleListResponse { Roles = roles });
    }

    private static async Task<IResult> CreateRole(
        CreateOrganizationRoleRequest request,
        HttpContext httpContext,
        OrganizationRoleService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.CreateRoleAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Json(new OrganizationRoleResponse { Role = result.Value! }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetRole(
        Guid roleId,
        HttpContext httpContext,
        OrganizationRoleService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.GetRoleAsync(currentUser.GetEffectiveOrganizationId()!.Value, roleId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new OrganizationRoleResponse { Role = result.Value! });
    }

    private static async Task<IResult> UpdateRole(
        Guid roleId,
        UpdateOrganizationRoleRequest request,
        HttpContext httpContext,
        OrganizationRoleService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.UpdateRoleAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, roleId, request, ReadIfMatch(httpContext),
            currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new OrganizationRoleResponse { Role = result.Value! });
    }

    private static async Task<IResult> DisableRole(
        Guid roleId,
        MaterialReasonRequest request,
        HttpContext httpContext,
        OrganizationRoleService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.DisableRoleAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, roleId, request, ReadIfMatch(httpContext),
            currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new OrganizationRoleResponse { Role = result.Value! });
    }

    private static async Task<IResult> RestoreRole(
        Guid roleId,
        MaterialReasonRequest request,
        HttpContext httpContext,
        OrganizationRoleService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.RestoreRoleAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, roleId, request, ReadIfMatch(httpContext),
            currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new OrganizationRoleResponse { Role = result.Value! });
    }

    private static async Task<IResult> UpdateGrants(
        Guid roleId,
        UpdateRoleGrantsRequest request,
        HttpContext httpContext,
        OrganizationRoleService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.UpdateGrantsAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, roleId, request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new OrganizationRoleResponse { Role = result.Value! });
    }

    private static async Task<IResult> ResetGrants(
        Guid roleId,
        MaterialReasonRequest request,
        HttpContext httpContext,
        OrganizationRoleService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ResetGrantsAsync(
            currentUser.GetEffectiveOrganizationId()!.Value, roleId, request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new OrganizationRoleResponse { Role = result.Value! });
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
