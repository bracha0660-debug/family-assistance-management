using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class AssistanceItemService(
    AppDbContext db,
    IAuditService auditService,
    AssistanceItemHistoryService historyService)
{
    private const string MaterialReasonRequiredMessage = "יש לציין סיבה לשינוי מהותי";
    private const string InvalidTransitionMessage = "מעבר סטטוס לא חוקי לפריט הסיוע";

    public async Task<ServiceResult<AssistanceItemListResponse>> ListAsync(
        Guid organizationId,
        AuthorizationContext auth,
        AssistanceItemListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new AssistanceItemListQuery();

        var baseQuery = ScopeEvaluator.ApplyAssistanceItemListScope(
            db.AssistanceItems
                .Include(i => i.AssistanceType)
                .Include(i => i.Supplier)
                .Include(i => i.PaymentExecution)
                .Include(i => i.CommitteeDecision)!
                    .ThenInclude(d => d!.Family)
                .Include(i => i.CommitteeDecision)!
                    .ThenInclude(d => d!.CreatedByUser)
                .Where(i => i.OrganizationId == organizationId
                    && i.Status != AssistanceItemStatuses.Draft),
            auth,
            PermissionKeys.CommitteeDecisionsView);

        if (query.Ownership == "mine")
            baseQuery = WorkflowHelpers.ApplyItemOwnershipMine(baseQuery, auth.UserId);

        if (!string.IsNullOrWhiteSpace(query.Status))
            baseQuery = baseQuery.Where(i => i.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Section))
            baseQuery = ApplySectionFilter(baseQuery, query.Section, auth);

        if (query.MinAgeDays is > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-query.MinAgeDays.Value);
            baseQuery = baseQuery.Where(i =>
                (i.Status != AssistanceItemStatuses.Submitted
                    || (i.CommitteeDecision!.SubmittedAt != null && i.CommitteeDecision.SubmittedAt < cutoff))
                && (i.Status != AssistanceItemStatuses.Suspended
                    || i.UpdatedAt < cutoff)
                && (i.Status != AssistanceItemStatuses.WaitingForReference
                    || i.UpdatedAt < cutoff));
        }

        var items = await baseQuery
            .OrderByDescending(i => i.UpdatedAt)
            .Skip(Math.Max(0, query.Offset))
            .Take(Math.Clamp(query.Limit, 1, 200))
            .ToListAsync(cancellationToken);

        var dtos = items.Select(i => MapListItem(i, auth)).ToList();
        return ServiceResult<AssistanceItemListResponse>.Ok(new AssistanceItemListResponse { Items = dtos });
    }

    public Task<ServiceResult<AssistanceItemListDto>> ApproveAsync(
        Guid organizationId, Guid id, StatusTransitionRequest? request, AuthorizationContext auth,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(organizationId, id, auth, PermissionKeys.CommitteeDecisionsApprove,
            [AssistanceItemStatuses.Submitted, AssistanceItemStatuses.Suspended],
            AssistanceItemStatuses.Approved, request?.Reason, optionalReason: true,
            (item, now) =>
            {
                item.ApprovedAt = now;
                // Snapshot once; never overwrite if already set (idempotent / backfill-safe).
                item.OriginalApprovedAmount ??= item.Amount;
                // Option A / Phase 16: do not create PaymentExecution on approve
            }, cancellationToken);

    public Task<ServiceResult<AssistanceItemListDto>> RejectAsync(
        Guid organizationId, Guid id, StatusTransitionRequest request, AuthorizationContext auth,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(organizationId, id, auth, PermissionKeys.CommitteeDecisionsReject,
            [AssistanceItemStatuses.Submitted, AssistanceItemStatuses.Suspended],
            AssistanceItemStatuses.Rejected, request.Reason, optionalReason: false,
            null, cancellationToken);

    public Task<ServiceResult<AssistanceItemListDto>> ReturnAsync(
        Guid organizationId, Guid id, StatusTransitionRequest request, AuthorizationContext auth,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(organizationId, id, auth, PermissionKeys.CommitteeDecisionsReject,
            [AssistanceItemStatuses.Submitted, AssistanceItemStatuses.Suspended],
            AssistanceItemStatuses.Returned, request.Reason, optionalReason: false,
            null, cancellationToken);

    public async Task<ServiceResult<AssistanceItemListDto>> SuspendAsync(
        Guid organizationId, Guid id, StatusTransitionRequest request, AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<AssistanceItemListDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var loaded = await LoadItemAsync(organizationId, id, cancellationToken);
        if (loaded is null)
            return ServiceResult<AssistanceItemListDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsApprove))
            return ServiceResult<AssistanceItemListDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (loaded.Status is not (AssistanceItemStatuses.Submitted or AssistanceItemStatuses.Approved))
            return ServiceResult<AssistanceItemListDto>.Fail(409, "INVALID_STATUS", InvalidTransitionMessage);

        return await ApplyTransitionAsync(loaded, AssistanceItemStatuses.Suspended, auth, reason, null, cancellationToken);
    }

    public async Task<ServiceResult<AssistanceItemListDto>> ResubmitAsync(
        Guid organizationId, Guid id, AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadItemAsync(organizationId, id, cancellationToken);
        if (loaded is null)
            return ServiceResult<AssistanceItemListDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsSubmit))
            return ServiceResult<AssistanceItemListDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (!WorkflowHelpers.IsDecisionOwnedByUser(loaded.CommitteeDecision!, auth.UserId))
            return ServiceResult<AssistanceItemListDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (loaded.Status != AssistanceItemStatuses.Returned)
            return ServiceResult<AssistanceItemListDto>.Fail(409, "INVALID_STATUS", InvalidTransitionMessage);

        return await ApplyTransitionAsync(loaded, AssistanceItemStatuses.Submitted, auth, null, null, cancellationToken);
    }

    public async Task<ServiceResult<AssistanceItemListDto>> SendToExecutionAsync(
        Guid organizationId, Guid id, AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadItemAsync(organizationId, id, cancellationToken);
        if (loaded is null)
            return ServiceResult<AssistanceItemListDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        // C10: send_to_execution / future batch create uses export_batches.create
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesCreate))
            return ServiceResult<AssistanceItemListDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (loaded.Status != AssistanceItemStatuses.Approved)
            return ServiceResult<AssistanceItemListDto>.Fail(409, "INVALID_STATUS", InvalidTransitionMessage);

        if (loaded.PaymentExecution is not null
            || await db.PaymentExecutions.AnyAsync(p => p.AssistanceItemId == loaded.Id, cancellationToken))
            return ServiceResult<AssistanceItemListDto>.Fail(409, "PAYMENT_EXISTS", "כבר קיים ביצוע תשלום לפריט זה");

        return await ApplyTransitionAsync(loaded, AssistanceItemStatuses.WaitingForReference, auth, null, (item, now) =>
        {
            item.ExecutionStatus = PaymentExecutionStatuses.WaitingForReference;
            var payment = new PaymentExecution
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CommitteeDecisionId = item.CommitteeDecisionId,
                AssistanceItemId = item.Id,
                Status = PaymentExecutionStatuses.WaitingForReference,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.PaymentExecutions.Add(payment);
            item.PaymentExecution = payment;
        }, cancellationToken);
    }

    public async Task<ServiceResult<AssistanceItemListDto>> EnterReferenceAsync(
        Guid organizationId, Guid id, EnterReferenceRequest request, AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var reference = request.Reference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
            return ServiceResult<AssistanceItemListDto>.Fail(409, "VALIDATION_ERROR", "יש להזין אסמכתא");
        if (reference.Length > 200)
            return ServiceResult<AssistanceItemListDto>.Fail(400, "VALIDATION_ERROR", "אסמכתא ארוכה מדי");

        var loaded = await LoadItemAsync(organizationId, id, cancellationToken);
        if (loaded is null)
            return ServiceResult<AssistanceItemListDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsEnterReference))
            return ServiceResult<AssistanceItemListDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (loaded.Status != AssistanceItemStatuses.WaitingForReference)
            return ServiceResult<AssistanceItemListDto>.Fail(409, "INVALID_STATUS", InvalidTransitionMessage);

        return await ApplyTransitionAsync(loaded, AssistanceItemStatuses.Paid, auth, null, (item, now) =>
        {
            item.ExecutionReference = reference;
            item.ExecutionStatus = PaymentExecutionStatuses.Paid;
            var payment = item.PaymentExecution
                ?? db.PaymentExecutions.Local.FirstOrDefault(p => p.AssistanceItemId == item.Id);
            if (payment is not null)
            {
                payment.ExecutionReference = reference;
                payment.Status = PaymentExecutionStatuses.Paid;
                payment.PaidAt = now;
                payment.UpdatedAt = now;
                payment.Version++;
            }
        }, cancellationToken);
    }

    public Task<ServiceResult<AssistanceItemListDto>> CompleteAsync(
        Guid organizationId, Guid id, AuthorizationContext auth,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(organizationId, id, auth, PermissionKeys.AssistanceItemsComplete,
            [AssistanceItemStatuses.Paid], AssistanceItemStatuses.Completed, null, optionalReason: true,
            null, cancellationToken);

    private async Task<ServiceResult<AssistanceItemListDto>> TransitionAsync(
        Guid organizationId,
        Guid id,
        AuthorizationContext auth,
        string permissionKey,
        IReadOnlyCollection<string> fromStatuses,
        string toStatus,
        string? reason,
        bool optionalReason,
        Action<AssistanceItem, DateTime>? beforeSave,
        CancellationToken cancellationToken)
    {
        if (!optionalReason)
        {
            var trimmed = reason?.Trim() ?? string.Empty;
            if (trimmed.Length < 3 || trimmed.Length > 500)
                return ServiceResult<AssistanceItemListDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);
            reason = trimmed;
        }
        else
        {
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        }

        if (!PermissionService.HasWorkflowGrant(auth, permissionKey))
            return ServiceResult<AssistanceItemListDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var loaded = await LoadItemAsync(organizationId, id, cancellationToken);
        if (loaded is null)
            return ServiceResult<AssistanceItemListDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (!fromStatuses.Contains(loaded.Status))
            return ServiceResult<AssistanceItemListDto>.Fail(409, "INVALID_STATUS", InvalidTransitionMessage);

        return await ApplyTransitionAsync(loaded, toStatus, auth, reason, beforeSave, cancellationToken);
    }

    private async Task<ServiceResult<AssistanceItemListDto>> ApplyTransitionAsync(
        AssistanceItem item,
        string newStatus,
        AuthorizationContext auth,
        string? reason,
        Action<AssistanceItem, DateTime>? beforeSave,
        CancellationToken cancellationToken)
    {
        var oldStatus = item.Status;
        var now = DateTime.UtcNow;
        item.Status = newStatus;
        item.Version++;
        item.UpdatedAt = now;
        beforeSave?.Invoke(item, now);

        var decision = item.CommitteeDecision!;
        decision.Status = WorkflowHelpers.ComputeDerivedDecisionStatus(decision);
        decision.UpdatedAt = now;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var historyType = newStatus switch
            {
                AssistanceItemStatuses.Approved => AssistanceItemHistoryEventTypes.Approved,
                AssistanceItemStatuses.Rejected => AssistanceItemHistoryEventTypes.Rejected,
                AssistanceItemStatuses.Returned => AssistanceItemHistoryEventTypes.Returned,
                AssistanceItemStatuses.Suspended => AssistanceItemHistoryEventTypes.Suspended,
                AssistanceItemStatuses.Submitted when oldStatus == AssistanceItemStatuses.Returned
                    => AssistanceItemHistoryEventTypes.Resubmitted,
                AssistanceItemStatuses.Submitted => AssistanceItemHistoryEventTypes.Submitted,
                AssistanceItemStatuses.WaitingForReference => AssistanceItemHistoryEventTypes.ExportBatchCreated,
                AssistanceItemStatuses.Paid when oldStatus == AssistanceItemStatuses.WaitingForReference
                    => AssistanceItemHistoryEventTypes.ReferenceEntered,
                AssistanceItemStatuses.Paid => AssistanceItemHistoryEventTypes.MarkedPaid,
                AssistanceItemStatuses.Completed => AssistanceItemHistoryEventTypes.ProcessCompleted,
                _ => AssistanceItemHistoryEventTypes.ItemEdited
            };

            await historyService.AppendEventAsync(
                item.OrganizationId,
                item.Id,
                historyType,
                auth.UserId,
                null,
                null,
                null,
                reason,
                [
                    AssistanceItemHistoryService.CreateFieldChange(
                        AssistanceItemEditableFields.Status,
                        oldStatus,
                        newStatus)
                ],
                now,
                cancellationToken);

            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceItemStatusChange,
                OrganizationId = item.OrganizationId,
                ActorUserId = auth.UserId,
                EntityType = "assistance_item",
                EntityId = item.Id,
                Action = newStatus,
                FieldName = "status",
                OldValue = oldStatus,
                NewValue = newStatus,
                Reason = reason
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<AssistanceItemListDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<AssistanceItemListDto>.Ok(MapListItem(item, auth));
    }

    private async Task<AssistanceItem?> LoadItemAsync(
        Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        await db.AssistanceItems
            .Include(i => i.AssistanceType)
            .Include(i => i.Supplier)
            .Include(i => i.PaymentExecution)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Family)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.CreatedByUser)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == organizationId, cancellationToken);

    private static IQueryable<AssistanceItem> ApplySectionFilter(
        IQueryable<AssistanceItem> query, string section, AuthorizationContext auth)
    {
        return section switch
        {
            "waiting_my_approval" or "my_waiting_manager_approval" =>
                query.Where(i => i.Status == AssistanceItemStatuses.Submitted),
            "manager_returned" or "my_returned_for_revision" =>
                query.Where(i => i.Status == AssistanceItemStatuses.Returned),
            "manager_suspended" or "my_suspended" =>
                query.Where(i => i.Status == AssistanceItemStatuses.Suspended),
            "approved" or "my_in_finance_execution" =>
                query.Where(i => i.Status == AssistanceItemStatuses.Approved
                    || i.Status == AssistanceItemStatuses.WaitingForReference),
            "finance_awaiting_execution" =>
                query.Where(i => i.Status == AssistanceItemStatuses.WaitingForReference),
            "my_paid_completed" or "finance_paid" =>
                query.Where(i => i.Status == AssistanceItemStatuses.Paid
                    || i.Status == AssistanceItemStatuses.Completed),
            "manager_rejected" or "my_rejected" =>
                query.Where(i => i.Status == AssistanceItemStatuses.Rejected),
            _ => query
        };
    }

    public static AssistanceItemListDto MapListItem(AssistanceItem i, AuthorizationContext auth)
    {
        var decision = i.CommitteeDecision!;
        return new AssistanceItemListDto
        {
            Id = i.Id,
            Status = i.Status,
            AvailableActions = WorkflowHelpers.AvailableAssistanceItemActions(i, decision, auth),
            DecisionId = decision.Id,
            DecisionCode = decision.DecisionCode,
            LineNumber = i.LineNumber,
            FamilyId = decision.FamilyId,
            FamilyCode = decision.Family?.FamilyCode ?? string.Empty,
            FamilyAccountingCode = decision.Family?.AccountingCode ?? 0,
            FamilyName = decision.Family?.FamilyLastName ?? string.Empty,
            AssistanceTypeId = i.AssistanceTypeId,
            AssistanceTypeName = i.AssistanceType?.Name ?? string.Empty,
            AssistanceTypeCode = i.AssistanceType?.TypeCode ?? string.Empty,
            Amount = i.Amount,
            OriginalApprovedAmount = i.OriginalApprovedAmount,
            PreviousPaymentAmount = i.PreviousPaymentAmount,
            AmountAdjustmentReason = i.AmountAdjustmentReason,
            AmountAdjustmentExplanation = i.AmountAdjustmentExplanation,
            Description = i.Description,
            PaymentTarget = i.PaymentTarget,
            PaymentMethod = i.PaymentMethod,
            SupplierId = i.SupplierId,
            SupplierName = i.Supplier?.Name,
            SupplierAccountingCode = i.Supplier?.AccountingCode,
            PayeeName = i.PayeeName,
            TransferBankNumber = i.TransferBankNumber,
            TransferBranchNumber = i.TransferBranchNumber,
            TransferAccountNumber = i.TransferAccountNumber,
            AccountHolderName = i.AccountHolderName,
            VoucherType = i.VoucherType,
            IsUrgent = i.IsUrgent,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            SubmittedAt = decision.SubmittedAt,
            ApprovedAt = i.ApprovedAt,
            ExecutionReference = i.ExecutionReference ?? i.PaymentExecution?.ExecutionReference,
            PaymentExecutionId = i.PaymentExecution?.Id,
            CreatedByUserId = decision.CreatedByUserId,
            CreatedByUserName = decision.CreatedByUser?.FullName ?? string.Empty,
            Version = i.Version
        };
    }
}
