using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class SuppliersEndpoints
{
    public static void MapSuppliersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/suppliers", List).RequirePermission(PermissionKeys.SuppliersView);
        group.MapPost("/suppliers", Create).RequirePermission(PermissionKeys.SuppliersCreate);
        group.MapGet("/suppliers/{id:guid}", Get).RequirePermission(PermissionKeys.SuppliersView);
        group.MapPatch("/suppliers/{id:guid}", Update).RequirePermission(PermissionKeys.SuppliersEdit);
        group.MapPatch("/suppliers/{id:guid}/deactivate", Deactivate).RequirePermission(PermissionKeys.SuppliersDeactivate);
        group.MapPatch("/suppliers/{id:guid}/restore", Restore).RequirePermission(PermissionKeys.SuppliersRestore);
    }

    private static Guid GetOrgId(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!.EffectiveOrganizationId!.Value;

    private static async Task<IResult> List(
        HttpContext httpContext,
        SupplierService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(GetOrgId(httpContext), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateSupplierRequest request,
        HttpContext httpContext,
        SupplierService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var result = await service.CreateAsync(GetOrgId(httpContext), request, auth.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Json(new SupplierResponse { Supplier = result.Value! }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Get(
        Guid id,
        HttpContext httpContext,
        SupplierService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(GetOrgId(httpContext), id, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new SupplierResponse { Supplier = result.Value! });
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateSupplierRequest request,
        HttpContext httpContext,
        SupplierService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var result = await service.UpdateAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), auth.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new SupplierResponse { Supplier = result.Value! });
    }

    private static async Task<IResult> Deactivate(
        Guid id,
        DeactivateSupplierRequest request,
        HttpContext httpContext,
        SupplierService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var result = await service.DeactivateAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), auth.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new SupplierResponse { Supplier = result.Value! });
    }

    private static async Task<IResult> Restore(
        Guid id,
        RestoreSupplierRequest request,
        HttpContext httpContext,
        SupplierService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var result = await service.RestoreAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), auth.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new SupplierResponse { Supplier = result.Value! });
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
            new ApiError
            {
                Error = result.Error,
                Code = result.Code,
                Details = result.StructuredDetails ?? result.Details
            },
            statusCode: result.StatusCode);
}
