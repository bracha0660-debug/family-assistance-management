using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class CommitteeDecisionsEndpoints
{
    public static void MapCommitteeDecisionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/committee-decisions", List).RequirePermission(PermissionKeys.CommitteeDecisionsView);
        group.MapPost("/committee-decisions", Create).RequirePermission(PermissionKeys.CommitteeDecisionsCreate);
        group.MapGet("/committee-decisions/{id:guid}", Get).RequirePermission(PermissionKeys.CommitteeDecisionsView);
        group.MapPatch("/committee-decisions/{id:guid}", UpdateDraft).RequirePermission(PermissionKeys.CommitteeDecisionsEditDraft);
        group.MapPost("/committee-decisions/{id:guid}/submit", Submit).RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsSubmit);
        group.MapPost("/committee-decisions/{id:guid}/approve", Approve).RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsApprove);
        group.MapPost("/committee-decisions/{id:guid}/reject", Reject).RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsReject);
        group.MapPost("/committee-decisions/{id:guid}/suspend", Suspend).RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsApprove);
        group.MapPost("/committee-decisions/{id:guid}/resume", Resume).RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsApprove);
        group.MapPost("/committee-decisions/{id:guid}/cancel", Cancel).RequireWorkflowPermission(PermissionKeys.CommitteeDecisionsCancel);
        group.MapPost("/committee-decisions/{id:guid}/items", AddItem).RequirePermission(PermissionKeys.AssistanceItemsCreate);
        group.MapPatch("/committee-decisions/{decisionId:guid}/items/{itemId:guid}", UpdateItem).RequirePermission(PermissionKeys.AssistanceItemsEdit);
        group.MapDelete("/committee-decisions/{decisionId:guid}/items/{itemId:guid}", RemoveItem).RequirePermission(PermissionKeys.AssistanceItemsRemoveDraft);
    }

    private static Guid GetOrgId(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!.EffectiveOrganizationId!.Value;

    private static AuthorizationContext GetAuth(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!;

    private static async Task<IResult> List(
        HttpContext httpContext,
        CommitteeDecisionService service,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? status,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? workflowPhase,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? ownership,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? section,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? q,
        [Microsoft.AspNetCore.Mvc.FromQuery] Guid? familyId,
        [Microsoft.AspNetCore.Mvc.FromQuery] int? minAgeDays,
        [Microsoft.AspNetCore.Mvc.FromQuery] int limit = 50,
        [Microsoft.AspNetCore.Mvc.FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var query = new CommitteeDecisionListQuery
        {
            Status = status,
            WorkflowPhase = workflowPhase,
            Ownership = ownership,
            Section = section,
            Q = q,
            FamilyId = familyId,
            MinAgeDays = minAgeDays,
            Limit = limit,
            Offset = offset
        };
        var result = await service.ListAsync(GetOrgId(httpContext), GetAuth(httpContext), query, cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> Create(
        CreateCommitteeDecisionRequest request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(GetOrgId(httpContext), request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Json(new CommitteeDecisionResponse { Decision = result.Value! }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Get(
        Guid id,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(GetOrgId(httpContext), id, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
    }

    private static async Task<IResult> UpdateDraft(
        Guid id,
        UpdateCommitteeDecisionRequest request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateDraftAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
    }

    private static async Task<IResult> Submit(
        Guid id,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SubmitAsync(
            GetOrgId(httpContext), id, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
    }

    private static async Task<IResult> Approve(
        Guid id,
        ApproveCommitteeDecisionRequest? request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
    }

    private static async Task<IResult> Reject(
        Guid id,
        RejectCommitteeDecisionRequest request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
    }

    private static async Task<IResult> Suspend(
        Guid id,
        StatusTransitionRequest request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SuspendAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
    }

    private static async Task<IResult> Resume(
        Guid id,
        ResumeCommitteeDecisionRequest? request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ResumeAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
    }

    private static async Task<IResult> Cancel(
        Guid id,
        StatusTransitionRequest request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CancelAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
    }

    private static async Task<IResult> AddItem(
        Guid id,
        CreateAssistanceItemRequest request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.AddItemAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Json(
            new AssistanceItemResponse { Item = result.Value!.Item, DecisionVersion = result.Value!.DecisionVersion },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateItem(
        Guid decisionId,
        Guid itemId,
        UpdateAssistanceItemRequest request,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateItemAsync(
            GetOrgId(httpContext), decisionId, itemId, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemResponse { Item = result.Value! });
    }

    private static async Task<IResult> RemoveItem(
        Guid decisionId,
        Guid itemId,
        HttpContext httpContext,
        CommitteeDecisionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RemoveItemAsync(
            GetOrgId(httpContext), decisionId, itemId, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new CommitteeDecisionResponse { Decision = result.Value! });
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
