using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Endpoints;

public static class PaymentsEndpoints
{
    public static void MapPaymentsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/org");

        group.MapGet("/payments", List).RequirePermission(PermissionKeys.PaymentsView);
        group.MapGet("/payments/{id:guid}", Get).RequirePermission(PermissionKeys.PaymentsView);
        group.MapPost("/payments/{id:guid}/execute", Execute).RequireWorkflowPermission(PermissionKeys.PaymentsExecute);
        group.MapPost("/payments/{id:guid}/proof", UploadProof).RequireWorkflowPermission(PermissionKeys.PaymentsUploadProof);
        group.MapPatch("/payments/{id:guid}/mark-paid", MarkPaid).RequireWorkflowPermission(PermissionKeys.PaymentsMarkPaid);
        group.MapPost("/payments/{id:guid}/return-to-coordinator", ReturnToCoordinator).RequireWorkflowPermission(PermissionKeys.PaymentsReturnToCoordinator);
    }

    private static Guid GetOrgId(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!.EffectiveOrganizationId!.Value;

    private static AuthorizationContext GetAuth(HttpContext httpContext) =>
        httpContext.GetAuthorizationContext()!;

    private static async Task<IResult> List(
        HttpContext httpContext,
        PaymentService service,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? status,
        [Microsoft.AspNetCore.Mvc.FromQuery] string? section,
        [Microsoft.AspNetCore.Mvc.FromQuery] bool urgentOnly = false,
        [Microsoft.AspNetCore.Mvc.FromQuery] int? minAgeDays = null,
        [Microsoft.AspNetCore.Mvc.FromQuery] int limit = 50,
        [Microsoft.AspNetCore.Mvc.FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var query = new PaymentListQuery
        {
            Status = status,
            Section = section,
            UrgentOnly = urgentOnly,
            MinAgeDays = minAgeDays,
            Limit = limit,
            Offset = offset
        };
        var result = await service.ListQueueAsync(GetOrgId(httpContext), GetAuth(httpContext), query, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Get(
        Guid id,
        HttpContext httpContext,
        PaymentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(GetOrgId(httpContext), id, GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new PaymentResponse { Payment = result.Value! });
    }

    private static async Task<IResult> Execute(
        Guid id,
        ExecutePaymentRequest request,
        HttpContext httpContext,
        PaymentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new PaymentResponse { Payment = result.Value! });
    }

    private static async Task<IResult> UploadProof(
        Guid id,
        HttpContext httpContext,
        PaymentService service,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.HasFormContentType)
            return Results.Json(new ApiError { Error = "נדרשת העלאת קובץ", Code = "VALIDATION_ERROR" }, statusCode: 400);

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            return Results.Json(new ApiError { Error = "קובץ הוא שדה חובה", Code = "VALIDATION_ERROR" }, statusCode: 400);

        var metadata = new UploadProofMetadata
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length
        };

        await using var stream = file.OpenReadStream();
        var result = await service.UploadProofAsync(
            GetOrgId(httpContext), id, metadata, stream, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new PaymentResponse { Payment = result.Value! });
    }

    private static async Task<IResult> MarkPaid(
        Guid id,
        MarkPaidRequest request,
        HttpContext httpContext,
        PaymentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.MarkPaidAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new PaymentResponse { Payment = result.Value! });
    }

    private static async Task<IResult> ReturnToCoordinator(
        Guid id,
        ReturnPaymentRequest request,
        HttpContext httpContext,
        PaymentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ReturnToCoordinatorAsync(
            GetOrgId(httpContext), id, request, ReadIfMatch(httpContext), GetAuth(httpContext), cancellationToken);
        if (!result.IsSuccess)
            return ToError(result);
        return Results.Ok(new PaymentResponse { Payment = result.Value! });
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
