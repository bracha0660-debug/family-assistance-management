using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class FamiliesEndpoints
{
    public static void MapFamiliesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/families", ListFamilies).RequireFamilyViewer();
        group.MapPost("/families", CreateFamily).RequireCoordinator();
        group.MapGet("/families/{id:guid}", GetFamily).RequireFamilyViewer();
        group.MapPatch("/families/{id:guid}", UpdateFamily).RequireCoordinator();
        group.MapPatch("/families/{id:guid}/deactivate", DeactivateFamily).RequireCoordinator();
    }

    private static async Task<IResult> ListFamilies(
        HttpContext httpContext,
        FamilyService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ListFamiliesAsync(
            currentUser.OrganizationId!.Value,
            currentUser.Role,
            currentUser.UserId,
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateFamily(
        CreateFamilyRequest request,
        HttpContext httpContext,
        FamilyService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.CreateFamilyAsync(
            currentUser.OrganizationId!.Value,
            request,
            currentUser.UserId,
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Json(
            new FamilyResponse { Family = result.Value! },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetFamily(
        Guid id,
        HttpContext httpContext,
        FamilyService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.GetFamilyAsync(
            currentUser.OrganizationId!.Value,
            id,
            currentUser.Role,
            currentUser.UserId,
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new FamilyResponse { Family = result.Value! });
    }

    private static async Task<IResult> UpdateFamily(
        Guid id,
        UpdateFamilyRequest request,
        HttpContext httpContext,
        FamilyService service,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch(httpContext);
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.UpdateFamilyAsync(
            currentUser.OrganizationId!.Value,
            id,
            request,
            version,
            currentUser.UserId,
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new FamilyResponse { Family = result.Value! });
    }

    private static async Task<IResult> DeactivateFamily(
        Guid id,
        DeactivateFamilyRequest request,
        HttpContext httpContext,
        FamilyService service,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch(httpContext);
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.DeactivateFamilyAsync(
            currentUser.OrganizationId!.Value,
            id,
            request,
            version,
            currentUser.UserId,
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new FamilyResponse { Family = result.Value! });
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
