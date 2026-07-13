using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class ExportBatchesEndpoints
{
    public static void MapExportBatchesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/payment-rows", ListPaymentRows).RequirePermission(PermissionKeys.PaymentsView);
        group.MapGet("/payment-rows/{assistanceItemId:guid}", GetPaymentRow).RequirePermission(PermissionKeys.PaymentsView);
        group.MapPost("/payment-rows/{assistanceItemId:guid}/enter-reference", EnterReference)
            .RequireWorkflowPermission(PermissionKeys.PaymentsEnterReference);
        group.MapPost("/payment-rows/{assistanceItemId:guid}/adjust-amount", AdjustAmount)
            .RequireWorkflowPermission(PermissionKeys.PaymentsEditAssistanceItems);
        group.MapPost("/payment-rows/{assistanceItemId:guid}/edit", EditPaymentDetails)
            .RequireWorkflowPermission(PermissionKeys.PaymentsEditAssistanceItems);
        group.MapGet("/assistance-items/{assistanceItemId:guid}/history", GetAssistanceItemHistory)
            .RequirePermission(PermissionKeys.AssistanceItemsViewHistory);

        group.MapGet("/export-batches", ListBatches).RequirePermission(PermissionKeys.PaymentsView);
        group.MapPost("/export-batches", CreateBatch)
            .RequireWorkflowPermission(PermissionKeys.PaymentsExportBatchesCreate);
        group.MapGet("/export-batches/{id:guid}", GetBatch).RequirePermission(PermissionKeys.PaymentsView);
        group.MapGet("/export-batches/{id:guid}/download", DownloadBatch)
            .RequireWorkflowPermission(PermissionKeys.PaymentsExportBatchesDownload);
        group.MapPost("/export-batches/{id:guid}/cancel", CancelBatch)
            .RequireWorkflowPermission(PermissionKeys.PaymentsExportBatchesCancel);
        group.MapPost("/export-batches/{id:guid}/items/{itemId:guid}/cancel", CancelBatchItem)
            .RequireWorkflowPermission(PermissionKeys.PaymentsExportBatchItemsCancel);
    }

    private static Guid GetOrgId(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!.EffectiveOrganizationId!.Value;

    private static AuthorizationContext GetAuth(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!;

    private static async Task<IResult> ListPaymentRows(
        HttpContext httpContext,
        ExportBatchService service,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? status,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? section,
        [Microsoft.AspNetCore.Mvc.FromQuery] int? minAgeDays = null,
        [Microsoft.AspNetCore.Mvc.FromQuery] int limit = 50,
        [Microsoft.AspNetCore.Mvc.FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListPaymentRowsAsync(
            GetOrgId(httpContext),
            GetAuth(httpContext),
            new PaymentRowListQuery
            {
                Status = status,
                Section = section,
                MinAgeDays = minAgeDays,
                Limit = limit,
                Offset = offset
            },
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetPaymentRow(
        Guid assistanceItemId,
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPaymentRowAsync(
            GetOrgId(httpContext), assistanceItemId, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new PaymentRowResponse { Item = result.Value! });
    }

    private static async Task<IResult> EnterReference(
        Guid assistanceItemId,
        EnterReferenceRequest request,
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.EnterReferenceAsync(
            GetOrgId(httpContext), assistanceItemId, request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new AssistanceItemWorkflowResponse { Item = result.Value! });
    }

    private static async Task<IResult> AdjustAmount(
        Guid assistanceItemId,
        AdjustPaymentAmountRequest request,
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.AdjustAmountAsync(
            GetOrgId(httpContext),
            assistanceItemId,
            request,
            ReadIfMatch(httpContext),
            GetAuth(httpContext),
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new PaymentRowResponse { Item = result.Value! });
    }

    private static async Task<IResult> EditPaymentDetails(
        Guid assistanceItemId,
        EditAssistanceItemPaymentRequest request,
        HttpContext httpContext,
        AssistanceItemPaymentEditService service,
        CancellationToken cancellationToken)
    {
        var result = await service.EditAsync(
            GetOrgId(httpContext),
            assistanceItemId,
            request,
            ReadIfMatch(httpContext),
            GetAuth(httpContext),
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new PaymentRowResponse { Item = result.Value! });
    }

    private static async Task<IResult> GetAssistanceItemHistory(
        Guid assistanceItemId,
        HttpContext httpContext,
        AssistanceItemHistoryService service,
        [Microsoft.AspNetCore.Mvc.FromQuery] int limit = 25,
        [Microsoft.AspNetCore.Mvc.FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListAsync(
            GetOrgId(httpContext),
            assistanceItemId,
            new AssistanceItemHistoryListQuery { Limit = limit, Offset = offset },
            GetAuth(httpContext),
            cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ListBatches(
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListBatchesAsync(GetOrgId(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateBatch(
        CreateExportBatchRequest request,
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateBatchAsync(
            GetOrgId(httpContext), request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new ExportBatchResponse { Batch = result.Value! });
    }

    private static async Task<IResult> GetBatch(
        Guid id,
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetBatchAsync(GetOrgId(httpContext), id, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new ExportBatchResponse { Batch = result.Value! });
    }

    private static async Task<IResult> DownloadBatch(
        Guid id,
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DownloadBatchAsync(
            GetOrgId(httpContext), id, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);

        var (content, fileName, contentType) = result.Value!;
        return Results.File(content, contentType, fileName);
    }

    private static async Task<IResult> CancelBatch(
        Guid id,
        CancelExportBatchRequest request,
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CancelBatchAsync(
            GetOrgId(httpContext), id, request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new ExportBatchResponse { Batch = result.Value! });
    }

    private static async Task<IResult> CancelBatchItem(
        Guid id,
        Guid itemId,
        CancelExportBatchItemRequest request,
        HttpContext httpContext,
        ExportBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CancelBatchItemAsync(
            GetOrgId(httpContext), id, itemId, request, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new ExportBatchResponse { Batch = result.Value! });
    }

    private static int? ReadIfMatch(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("If-Match", out var ifMatch) &&
            int.TryParse(ifMatch.ToString(), out var parsed))
            return parsed;
        return null;
    }

    private static IResult ToError<T>(ServiceResult<T> result)
    {
        object body = result.StructuredDetails is not null
            ? new ApiError
            {
                Error = result.Error,
                Code = result.Code,
                Details = result.StructuredDetails
            }
            : new ApiError
            {
                Error = result.Error,
                Code = result.Code,
                Details = result.Details.Count > 0 ? result.Details : null
            };
        return Results.Json(body, statusCode: result.StatusCode);
    }
}
