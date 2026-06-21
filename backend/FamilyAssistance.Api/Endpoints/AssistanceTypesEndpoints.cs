using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class AssistanceTypesEndpoints
{
    public static void MapAssistanceTypesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/assistance-types", List).RequirePermission(PermissionKeys.AssistanceTypesView);
        group.MapPost("/assistance-types", Create).RequirePermission(PermissionKeys.AssistanceTypesCreate);
        group.MapGet("/assistance-types/{id:guid}", Get).RequirePermission(PermissionKeys.AssistanceTypesView);
        group.MapPatch("/assistance-types/{id:guid}", Update).RequirePermission(PermissionKeys.AssistanceTypesEdit);
        group.MapPatch("/assistance-types/{id:guid}/deactivate", Deactivate).RequirePermission(PermissionKeys.AssistanceTypesDeactivate);
    }

    private static Guid GetOrgId(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!.EffectiveOrganizationId!.Value;

    private static async Task<IResult> List(
        HttpContext httpContext,
        AssistanceTypeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(GetOrgId(httpContext), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateAssistanceTypeRequest request,
        HttpContext httpContext,
        AssistanceTypeService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var result = await service.CreateAsync(GetOrgId(httpContext), request, auth.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Json(
            new AssistanceTypeResponse { AssistanceType = result.Value! },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Get(
        Guid id,
        HttpContext httpContext,
        AssistanceTypeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(GetOrgId(httpContext), id, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new AssistanceTypeResponse { AssistanceType = result.Value! });
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateAssistanceTypeRequest request,
        HttpContext httpContext,
        AssistanceTypeService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var result = await service.UpdateAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), auth.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new AssistanceTypeResponse { AssistanceType = result.Value! });
    }

    private static async Task<IResult> Deactivate(
        Guid id,
        DeactivateAssistanceTypeRequest request,
        HttpContext httpContext,
        AssistanceTypeService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var result = await service.DeactivateAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), auth.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new AssistanceTypeResponse { AssistanceType = result.Value! });
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
