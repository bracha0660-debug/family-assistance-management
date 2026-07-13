using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class AssistanceItemsEndpoints
{
    public static void MapAssistanceItemsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/assistance-items", List).RequirePermission(PermissionKeys.CommitteeDecisionsView);
        group.MapPost("/assistance-items/{id:guid}/approve", Approve)
            .RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsApprove);
        group.MapPost("/assistance-items/{id:guid}/reject", Reject)
            .RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsReject);
        group.MapPost("/assistance-items/{id:guid}/return", Return)
            .RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsReject);
        group.MapPost("/assistance-items/{id:guid}/suspend", Suspend)
            .RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsApprove);
        group.MapPost("/assistance-items/{id:guid}/resubmit", Resubmit)
            .RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsSubmit);
        group.MapPost("/assistance-items/{id:guid}/send-to-execution", SendToExecution)
            .RequireWorkflowPermission(PermissionKeys.PaymentsExportBatchesCreate);
        group.MapPost("/assistance-items/{id:guid}/enter-reference", EnterReference)
            .RequireWorkflowPermission(PermissionKeys.PaymentsEnterReference);
        group.MapPost("/assistance-items/{id:guid}/complete", Complete)
            .RequireWorkflowPermission(PermissionKeys.AssistanceItemsComplete);
    }

    private static Guid GetOrgId(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!.EffectiveOrganizationId!.Value;

    private static AuthorizationContext GetAuth(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!;

    private static async Task<IResult> List(
        HttpContext httpContext,
        AssistanceItemService service,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? status,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? section,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? ownership,
        [Microsoft.AspNetCore.Mvc.FromQuery] int? minAgeDays,
        [Microsoft.AspNetCore.Mvc.FromQuery] int limit = 50,
        [Microsoft.AspNetCore.Mvc.FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var query = new AssistanceItemListQuery
        {
            Status = status,
            Section = section,
            Ownership = ownership,
            MinAgeDays = minAgeDays,
            Limit = limit,
            Offset = offset
        };
        var result = await service.ListAsync(GetOrgId(httpContext), GetAuth(httpContext), query, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> Approve(
        Guid id,
        StatusTransitionRequest? request,
        HttpContext httpContext,
        AssistanceItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveAsync(GetOrgId(httpContext), id, request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static async Task<IResult> Reject(
        Guid id,
        StatusTransitionRequest request,
        HttpContext httpContext,
        AssistanceItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectAsync(GetOrgId(httpContext), id, request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static async Task<IResult> Return(
        Guid id,
        StatusTransitionRequest request,
        HttpContext httpContext,
        AssistanceItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ReturnAsync(GetOrgId(httpContext), id, request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static async Task<IResult> Suspend(
        Guid id,
        StatusTransitionRequest request,
        HttpContext httpContext,
        AssistanceItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SuspendAsync(GetOrgId(httpContext), id, request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static async Task<IResult> Resubmit(
        Guid id,
        HttpContext httpContext,
        AssistanceItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ResubmitAsync(GetOrgId(httpContext), id, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static async Task<IResult> SendToExecution(
        Guid id,
        HttpContext httpContext,
        AssistanceItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SendToExecutionAsync(GetOrgId(httpContext), id, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static async Task<IResult> EnterReference(
        Guid id,
        EnterReferenceRequest request,
        HttpContext httpContext,
        AssistanceItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.EnterReferenceAsync(
            GetOrgId(httpContext), id, request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static async Task<IResult> Complete(
        Guid id,
        HttpContext httpContext,
        AssistanceItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CompleteAsync(GetOrgId(httpContext), id, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static IResult ToError<T>(ServiceResult<T> result) =>
        Results.Json(
            new ApiError { Error = result.Error, Code = result.Code, Details = result.Details },
            statusCode: result.StatusCode);
}
