using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class AssistanceTypesEndpoints
{
    public static void MapAssistanceTypesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/assistance-types", List).RequireTypeViewer();
        group.MapPost("/assistance-types", Create).RequireFinance();
        group.MapGet("/assistance-types/{id:guid}", Get).RequireTypeViewer();
        group.MapPatch("/assistance-types/{id:guid}", Update).RequireFinance();
        group.MapPatch("/assistance-types/{id:guid}/deactivate", Deactivate).RequireFinance();
    }

    private static async Task<IResult> List(
        HttpContext httpContext,
        AssistanceTypeService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.ListAsync(currentUser.OrganizationId!.Value, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateAssistanceTypeRequest request,
        HttpContext httpContext,
        AssistanceTypeService service,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.CreateAsync(
            currentUser.OrganizationId!.Value, request, currentUser.UserId, cancellationToken);
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
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.GetAsync(currentUser.OrganizationId!.Value, id, cancellationToken);
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
        var version = ReadIfMatch(httpContext);
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.UpdateAsync(
            currentUser.OrganizationId!.Value, id, request, version, currentUser.UserId, cancellationToken);
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
        var version = ReadIfMatch(httpContext);
        var currentUser = httpContext.GetCurrentUser()!;
        var result = await service.DeactivateAsync(
            currentUser.OrganizationId!.Value, id, request, version, currentUser.UserId, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        return Results.Ok(new AssistanceTypeResponse { AssistanceType = result.Value! });
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
