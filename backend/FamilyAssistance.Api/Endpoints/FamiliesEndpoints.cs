using FamilyAssistance.Api.Auth;

using FamilyAssistance.Api.Constants;

using FamilyAssistance.Api.Models;

using FamilyAssistance.Api.Policies;

using FamilyAssistance.Api.Services;



namespace FamilyAssistance.Api.Endpoints;



public static class FamiliesEndpoints

{

    public static void MapFamiliesEndpoints(this WebApplication app)

    {

        var group = app.MapGroup("/api/v1/org");



        group.MapGet("/families", ListFamilies).RequirePermission(PermissionKeys.FamiliesView);

        group.MapGet("/families/suggested-accounting-code", GetSuggestedAccountingCode).RequirePermission(PermissionKeys.FamiliesCreate);

        group.MapPost("/families", CreateFamily).RequirePermission(PermissionKeys.FamiliesCreate);

        group.MapGet("/families/{id:guid}", GetFamily).RequirePermission(PermissionKeys.FamiliesView);

        group.MapPatch("/families/{id:guid}", UpdateFamily).RequirePermission(PermissionKeys.FamiliesEdit);

        group.MapPatch("/families/{id:guid}/deactivate", DeactivateFamily).RequirePermission(PermissionKeys.FamiliesDeactivate);

        group.MapPatch("/families/{id:guid}/restore", RestoreFamily).RequirePermission(PermissionKeys.FamiliesRestore);

    }



    private static async Task<IResult> ListFamilies(

        HttpContext httpContext,

        FamilyService service,

        CancellationToken cancellationToken)

    {

        var auth = httpContext.GetAuthorizationContext()!;

        var result = await service.ListFamiliesAsync(

            auth.EffectiveOrganizationId!.Value, auth, cancellationToken);

        if (!result.IsSuccess)

            return ToError(result);



        return Results.Ok(result.Value);

    }



    private static async Task<IResult> GetSuggestedAccountingCode(
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "coordinatorId")] Guid coordinatorId,
        HttpContext httpContext,
        FamilyService service,
        CancellationToken cancellationToken)
    {
        var auth = httpContext.GetAuthorizationContext()!;
        var result = await service.GetSuggestedAccountingCodeAsync(
            auth.EffectiveOrganizationId!.Value, coordinatorId, auth, cancellationToken);

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

        var auth = httpContext.GetAuthorizationContext()!;

        var result = await service.CreateFamilyAsync(

            auth.EffectiveOrganizationId!.Value, request, auth, cancellationToken);

        if (!result.IsSuccess)

            return ToError(result);



        return Results.Json(new FamilyResponse { Family = result.Value! }, statusCode: StatusCodes.Status201Created);

    }



    private static async Task<IResult> GetFamily(

        Guid id,

        HttpContext httpContext,

        FamilyService service,

        CancellationToken cancellationToken)

    {

        var auth = httpContext.GetAuthorizationContext()!;

        var result = await service.GetFamilyAsync(

            auth.EffectiveOrganizationId!.Value, id, auth, cancellationToken);

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

        var auth = httpContext.GetAuthorizationContext()!;

        var result = await service.UpdateFamilyAsync(

            auth.EffectiveOrganizationId!.Value, id, request, ReadIfMatch(httpContext), auth, cancellationToken);

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

        var auth = httpContext.GetAuthorizationContext()!;

        var result = await service.DeactivateFamilyAsync(

            auth.EffectiveOrganizationId!.Value, id, request, ReadIfMatch(httpContext), auth, cancellationToken);

        if (!result.IsSuccess)

            return ToError(result);



        return Results.Ok(new FamilyResponse { Family = result.Value! });

    }



    private static async Task<IResult> RestoreFamily(

        Guid id,

        RestoreFamilyRequest request,

        HttpContext httpContext,

        FamilyService service,

        CancellationToken cancellationToken)

    {

        var auth = httpContext.GetAuthorizationContext()!;

        var result = await service.RestoreFamilyAsync(

            auth.EffectiveOrganizationId!.Value, id, request, ReadIfMatch(httpContext), auth, cancellationToken);

        if (!result.IsSuccess)

            return ToError(result);



        return Results.Ok(new FamilyResponse { Family = result.Value! });

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


