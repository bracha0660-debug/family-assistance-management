using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Endpoints;

public static class AdminOrganizationsEndpoints
{
    public static void MapAdminOrganizationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin");

        group.MapGet("/organizations", ListOrganizations).RequireSuperAdmin();
        group.MapPost("/organizations", CreateOrganization).RequireSuperAdmin();
        group.MapPatch("/organizations/{id:guid}/suspend", SuspendOrganization).RequireSuperAdmin();
        group.MapPatch("/organizations/{id:guid}/restore", RestoreOrganization).RequireSuperAdmin();
        group.MapPost("/organizations/{id:guid}/admin", BootstrapOrgAdmin).RequireSuperAdmin();
        group.MapPost("/organizations/{id:guid}/enter", EnterOrganization).RequireSuperAdmin();
        group.MapPost("/organizations/{id:guid}/exit", ExitOrganization).RequireSuperAdmin();
    }

    private static async Task<IResult> ListOrganizations(
        OrganizationAdminService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListOrganizationsAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateOrganization(
        CreateOrganizationRequest request,
        HttpContext httpContext,
        OrganizationAdminService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.CreateOrganizationAsync(request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Json(new OrganizationResponse { Organization = result.Value! }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> SuspendOrganization(
        Guid id,
        SuspendOrganizationRequest request,
        HttpContext httpContext,
        OrganizationAdminService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.SuspendOrganizationAsync(
            id, request, ReadIfMatch(httpContext), currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new OrganizationResponse { Organization = result.Value! });
    }

    private static async Task<IResult> RestoreOrganization(
        Guid id,
        RestoreOrganizationRequest request,
        HttpContext httpContext,
        OrganizationAdminService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.RestoreOrganizationAsync(
            id, request, ReadIfMatch(httpContext), currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new OrganizationResponse { Organization = result.Value! });
    }

    private static async Task<IResult> BootstrapOrgAdmin(
        Guid id,
        BootstrapOrgAdminRequest request,
        HttpContext httpContext,
        OrganizationAdminService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.BootstrapOrgAdminAsync(id, request, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Json(new BootstrapUserResponse { User = result.Value! }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> EnterOrganization(
        Guid id,
        HttpContext httpContext,
        OrganizationAdminService service,
        AppDbContext db,
        UserDtoBuilder userDtoBuilder,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.EnterOrganizationAsync(
            id, currentUser.SessionId, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        var user = await db.Users
            .Include(u => u.Organization)
            .Include(u => u.OrganizationRole)
            .FirstAsync(u => u.Id == currentUser.UserId, cancellationToken);
        var session = await db.UserSessions.FindAsync([currentUser.SessionId], cancellationToken);
        var userDto = await userDtoBuilder.BuildAsync(user, session, cancellationToken);

        return Results.Ok(new LoginResponse { User = userDto });
    }

    private static async Task<IResult> ExitOrganization(
        Guid id,
        HttpContext httpContext,
        OrganizationAdminService service,
        AppDbContext db,
        UserDtoBuilder userDtoBuilder,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ExitOrganizationAsync(
            currentUser.SessionId, currentUser.UserId, id, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        var user = await db.Users
            .Include(u => u.Organization)
            .Include(u => u.OrganizationRole)
            .FirstAsync(u => u.Id == currentUser.UserId, cancellationToken);
        var session = await db.UserSessions.FindAsync([currentUser.SessionId], cancellationToken);
        var userDto = await userDtoBuilder.BuildAsync(user, session, cancellationToken);

        return Results.Ok(new LoginResponse { User = userDto });
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
