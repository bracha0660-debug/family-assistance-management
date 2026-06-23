using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class PaymentService(
    AppDbContext db,
    IAuditService auditService,
    DocumentStorageService documentStorage)
{
    private const string MaterialReasonRequiredMessage = "יש לציין סיבה לשינוי מהותי";

    private static readonly HashSet<string> QueueStatuses =
    [
        PaymentExecutionStatuses.AwaitingPayment,
        PaymentExecutionStatuses.Executing,
        PaymentExecutionStatuses.ProofUploaded
    ];

    public async Task<PaymentQueueListResponse> ListQueueAsync(
        Guid organizationId,
        AuthorizationContext auth,
        PaymentListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new PaymentListQuery();

        var paymentsQuery = db.PaymentExecutions
            .Include(p => p.AssistanceItem)
                .ThenInclude(i => i!.AssistanceType)
            .Include(p => p.AssistanceItem)
                .ThenInclude(i => i!.Supplier)
            .Include(p => p.CommitteeDecision)
                .ThenInclude(d => d!.Family)
            .Where(p => p.OrganizationId == organizationId);

        paymentsQuery = ApplyPaymentListFilters(paymentsQuery, query);

        var payments = await paymentsQuery
            .OrderBy(p => p.CreatedAt)
            .Skip(Math.Max(0, query.Offset))
            .Take(Math.Clamp(query.Limit, 1, 200))
            .ToListAsync(cancellationToken);

        var dtos = payments.Select(p => MapPayment(p, auth)).ToList();
        return new PaymentQueueListResponse
        {
            Summary = new PaymentQueueSummaryDto
            {
                Total = dtos.Count,
                AwaitingPayment = dtos.Count(p => p.Status == PaymentExecutionStatuses.AwaitingPayment),
                Executing = dtos.Count(p => p.Status == PaymentExecutionStatuses.Executing),
                ProofUploaded = dtos.Count(p => p.Status == PaymentExecutionStatuses.ProofUploaded),
                OnHold = dtos.Count(p => p.IsOnHold)
            },
            Payments = dtos
        };
    }

    public async Task<ServiceResult<PaymentQueueItemDto>> GetAsync(
        Guid organizationId,
        Guid id,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var payment = await LoadPaymentAsync(organizationId, id, cancellationToken);
        if (payment is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(404, "NOT_FOUND", "התשלום לא נמצא");

        return ServiceResult<PaymentQueueItemDto>.Ok(MapPayment(payment, auth));
    }

    public async Task<ServiceResult<PaymentQueueItemDto>> ExecuteAsync(
        Guid organizationId,
        Guid id,
        ExecutePaymentRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var payment = await LoadPaymentAsync(organizationId, id, cancellationToken);
        if (payment is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(404, "NOT_FOUND", "התשלום לא נמצא");

        if (payment.Status == PaymentExecutionStatuses.OnHold
            || payment.CommitteeDecision?.Status == CommitteeDecisionStatuses.Suspended)
            return ServiceResult<PaymentQueueItemDto>.Fail(403, "DECISION_ON_HOLD", "ההחלטה מושעה — לא ניתן לבצע תשלום");

        if (payment.Status != PaymentExecutionStatuses.AwaitingPayment)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "INVALID_STATUS", "לא ניתן לבצע תשלום במצב זה");

        if (payment.Version != expectedVersion)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var item = payment.AssistanceItem!;
        if (item.PaymentMethod == PaymentMethods.BankTransfer
            && item.PaymentTarget is PaymentTargets.Family or PaymentTargets.Supplier)
        {
            string? bankNumber;
            string? branchNumber;
            string? accountNumber;
            string? accountHolderName;

            if (item.PaymentTarget == PaymentTargets.Family)
            {
                var family = payment.CommitteeDecision!.Family!;
                bankNumber = family.BankNumber;
                branchNumber = family.BranchNumber;
                accountNumber = family.AccountNumber;
                accountHolderName = family.AccountHolderName;
            }
            else
            {
                if (item.Supplier is null)
                    return ServiceResult<PaymentQueueItemDto>.Fail(400, "INCOMPLETE_BANK_DETAILS",
                        BankFieldValidator.IncompleteBankForPaymentMessage);

                bankNumber = item.Supplier.BankNumber;
                branchNumber = item.Supplier.BranchNumber;
                accountNumber = item.Supplier.AccountNumber;
                accountHolderName = item.Supplier.AccountHolderName;
            }

            var bankErrors = BankFieldValidator.ValidateCompleteForPayment(
                bankNumber, branchNumber, accountNumber, accountHolderName);
            if (bankErrors.Count > 0)
                return ServiceResult<PaymentQueueItemDto>.Fail(400, "INCOMPLETE_BANK_DETAILS", bankErrors[0], bankErrors);
        }

        var now = DateTime.UtcNow;
        var reference = NormalizeOptional(request.ExecutionReference);
        payment.Status = PaymentExecutionStatuses.Executing;
        payment.ExecutionReference = reference;
        payment.ExecutedAt = now;
        payment.Version++;
        payment.UpdatedAt = now;
        payment.AssistanceItem!.ExecutionStatus = PaymentExecutionStatuses.Executing;
        payment.AssistanceItem.ExecutionReference = reference;
        payment.AssistanceItem.UpdatedAt = now;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.PaymentExecutionStarted,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "payment_execution",
                EntityId = payment.Id,
                Action = "payment_execute",
                FieldName = "status",
                OldValue = PaymentExecutionStatuses.AwaitingPayment,
                NewValue = PaymentExecutionStatuses.Executing
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PaymentQueueItemDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<PaymentQueueItemDto>.Ok(MapPayment(payment, auth));
    }

    public async Task<ServiceResult<PaymentQueueItemDto>> UploadProofAsync(
        Guid organizationId,
        Guid id,
        UploadProofMetadata metadata,
        Stream fileContent,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var payment = await LoadPaymentAsync(organizationId, id, cancellationToken);
        if (payment is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(404, "NOT_FOUND", "התשלום לא נמצא");

        if (payment.Status != PaymentExecutionStatuses.Executing)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "INVALID_STATUS", "לא ניתן להעלות אישור במצב זה");

        if (payment.Version != expectedVersion)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        if (string.IsNullOrWhiteSpace(metadata.FileName))
            return ServiceResult<PaymentQueueItemDto>.Fail(400, "VALIDATION_ERROR", "שם קובץ הוא שדה חובה");

        var (storedFileName, _) = await documentStorage.SaveAsync(
            organizationId, metadata.FileName, fileContent, cancellationToken);

        var now = DateTime.UtcNow;
        payment.ProofFileName = metadata.FileName.Trim();
        payment.ProofStoredFileName = storedFileName;
        payment.Status = PaymentExecutionStatuses.ProofUploaded;
        payment.ProofUploadedAt = now;
        payment.Version++;
        payment.UpdatedAt = now;
        payment.AssistanceItem!.ExecutionStatus = PaymentExecutionStatuses.ProofUploaded;
        payment.AssistanceItem.UpdatedAt = now;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.PaymentProofUploaded,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "payment_execution",
                EntityId = payment.Id,
                Action = "payment_upload_proof",
                FieldName = "proof_file_name",
                NewValue = payment.ProofFileName
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PaymentQueueItemDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<PaymentQueueItemDto>.Ok(MapPayment(payment, auth));
    }

    public async Task<ServiceResult<PaymentQueueItemDto>> MarkPaidAsync(
        Guid organizationId,
        Guid id,
        MarkPaidRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var payment = await LoadPaymentAsync(organizationId, id, cancellationToken);
        if (payment is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(404, "NOT_FOUND", "התשלום לא נמצא");

        if (payment.Status != PaymentExecutionStatuses.ProofUploaded)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "INVALID_STATUS", "יש להעלות אישור ביצוע לפני סימון כשולם");

        if (payment.Version != expectedVersion)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var now = DateTime.UtcNow;
        if (request.ExecutionReference is not null)
            payment.ExecutionReference = NormalizeOptional(request.ExecutionReference);

        payment.Status = PaymentExecutionStatuses.Paid;
        payment.PaidAt = now;
        payment.Version++;
        payment.UpdatedAt = now;
        payment.AssistanceItem!.ExecutionStatus = PaymentExecutionStatuses.Paid;
        payment.AssistanceItem.UpdatedAt = now;

        var decision = payment.CommitteeDecision!;
        await UpdateDecisionPaymentStatusAsync(decision, cancellationToken);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.PaymentMarkedPaid,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "payment_execution",
                EntityId = payment.Id,
                Action = "payment_mark_paid",
                FieldName = "status",
                OldValue = PaymentExecutionStatuses.ProofUploaded,
                NewValue = PaymentExecutionStatuses.Paid
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PaymentQueueItemDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<PaymentQueueItemDto>.Ok(MapPayment(payment, auth));
    }

    public async Task<ServiceResult<PaymentQueueItemDto>> ReturnToCoordinatorAsync(
        Guid organizationId,
        Guid id,
        ReturnPaymentRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<PaymentQueueItemDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var payment = await LoadPaymentAsync(organizationId, id, cancellationToken);
        if (payment is null)
            return ServiceResult<PaymentQueueItemDto>.Fail(404, "NOT_FOUND", "התשלום לא נמצא");

        if (payment.Status is PaymentExecutionStatuses.Paid or PaymentExecutionStatuses.ReturnedToCoordinator)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "INVALID_STATUS", "לא ניתן להחזיר תשלום במצב זה");

        if (payment.Version != expectedVersion)
            return ServiceResult<PaymentQueueItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var now = DateTime.UtcNow;
        var oldStatus = payment.Status;
        payment.Status = PaymentExecutionStatuses.ReturnedToCoordinator;
        payment.ReturnReason = reason;
        payment.ReturnedAt = now;
        payment.Version++;
        payment.UpdatedAt = now;
        payment.AssistanceItem!.ExecutionStatus = PaymentExecutionStatuses.ReturnedToCoordinator;
        payment.AssistanceItem.UpdatedAt = now;

        var decision = payment.CommitteeDecision!;
        decision.Status = CommitteeDecisionStatuses.ReturnedForRevision;
        decision.ReturnReason = reason;
        decision.Version++;
        decision.UpdatedAt = now;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.PaymentReturnedToCoordinator,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "payment_execution",
                EntityId = payment.Id,
                Action = "payment_return_to_coordinator",
                FieldName = "status",
                OldValue = oldStatus,
                NewValue = PaymentExecutionStatuses.ReturnedToCoordinator,
                Reason = reason
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PaymentQueueItemDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PaymentQueueItemDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<PaymentQueueItemDto>.Ok(MapPayment(payment, auth));
    }

    public PaymentQueueItemDto MapPaymentForDashboard(PaymentExecution payment, AuthorizationContext auth) =>
        MapPayment(payment, auth);

    private static IQueryable<PaymentExecution> ApplyPaymentListFilters(
        IQueryable<PaymentExecution> query,
        PaymentListQuery listQuery)
    {
        if (!string.IsNullOrWhiteSpace(listQuery.Status))
            return query.Where(p => p.Status == listQuery.Status);

        if (!string.IsNullOrWhiteSpace(listQuery.Section))
        {
            return listQuery.Section switch
            {
                "finance_on_hold" => query.Where(p =>
                    p.Status == PaymentExecutionStatuses.OnHold
                    || p.CommitteeDecision!.Status == CommitteeDecisionStatuses.Suspended),
                "finance_awaiting_execution" => query.Where(p =>
                    p.Status == PaymentExecutionStatuses.AwaitingPayment
                    && (p.CommitteeDecision!.Status == CommitteeDecisionStatuses.Approved
                        || p.CommitteeDecision.Status == CommitteeDecisionStatuses.PartiallyPaid)),
                "finance_executing" => query.Where(p => p.Status == PaymentExecutionStatuses.Executing),
                "finance_proof_uploaded" => query.Where(p => p.Status == PaymentExecutionStatuses.ProofUploaded),
                "finance_paid" => query.Where(p => p.Status == PaymentExecutionStatuses.Paid),
                "finance_returned" => query.Where(p => p.Status == PaymentExecutionStatuses.ReturnedToCoordinator),
                _ => query.Where(p => QueueStatuses.Contains(p.Status))
            };
        }

        if (listQuery.UrgentOnly)
            query = query.Where(p => p.AssistanceItem!.IsUrgent);

        if (listQuery.MinAgeDays is > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-listQuery.MinAgeDays.Value);
            query = query.Where(p => p.CreatedAt < cutoff);
        }

        return query.Where(p => QueueStatuses.Contains(p.Status));
    }

    private async Task UpdateDecisionPaymentStatusAsync(
        CommitteeDecision decision,
        CancellationToken cancellationToken)
    {
        var itemStatuses = await db.AssistanceItems
            .Where(i => i.CommitteeDecisionId == decision.Id)
            .Select(i => i.ExecutionStatus)
            .ToListAsync(cancellationToken);

        if (itemStatuses.All(s => s == PaymentExecutionStatuses.Paid))
            decision.Status = CommitteeDecisionStatuses.FullyPaid;
        else if (itemStatuses.Any(s => s == PaymentExecutionStatuses.Paid))
            decision.Status = CommitteeDecisionStatuses.PartiallyPaid;

        decision.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<PaymentExecution?> LoadPaymentAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken) =>
        await db.PaymentExecutions
            .Include(p => p.AssistanceItem)
                .ThenInclude(i => i!.AssistanceType)
            .Include(p => p.AssistanceItem)
                .ThenInclude(i => i!.Supplier)
            .Include(p => p.CommitteeDecision)
                .ThenInclude(d => d!.Family)
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, cancellationToken);

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static PaymentQueueItemDto MapPayment(PaymentExecution p, AuthorizationContext auth)
    {
        var item = p.AssistanceItem!;
        var decision = p.CommitteeDecision!;
        var family = decision.Family!;
        var isOnHold = p.Status == PaymentExecutionStatuses.OnHold
            || decision.Status == CommitteeDecisionStatuses.Suspended;
        return new PaymentQueueItemDto
        {
            Id = p.Id,
            CommitteeDecisionId = p.CommitteeDecisionId,
            DecisionCode = decision.DecisionCode,
            AssistanceItemId = p.AssistanceItemId,
            LineNumber = item.LineNumber,
            FamilyId = family.Id,
            FamilyCode = family.FamilyCode,
            FamilyLastName = family.FamilyLastName,
            AssistanceTypeName = item.AssistanceType?.Name ?? string.Empty,
            Amount = item.Amount,
            PaymentTarget = item.PaymentTarget,
            PaymentMethod = item.PaymentMethod,
            SupplierName = item.Supplier?.Name,
            PayeeName = item.PayeeName,
            Status = p.Status,
            DecisionStatus = decision.Status,
            SuspendReason = decision.SuspendReason,
            IsUrgent = item.IsUrgent,
            IsOnHold = isOnHold,
            ExecutionReference = p.ExecutionReference,
            ProofFileName = p.ProofFileName,
            AvailableActions = WorkflowHelpers.AvailablePaymentActions(p, auth),
            Version = p.Version,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
