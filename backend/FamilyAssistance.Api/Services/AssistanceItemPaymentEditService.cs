using System.Globalization;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

/// <summary>Phase B — closed allow-list edit for payment-queue AssistanceItems.</summary>
public sealed class AssistanceItemPaymentEditService(
    AppDbContext db,
    IAuditService auditService,
    AssistanceItemHistoryService historyService,
    ExportBatchService exportBatchService)
{
    public const string VersionConflictMessage = AssistanceItemHistoryService.VersionConflictMessage;

    public async Task<ServiceResult<PaymentRowDto>> EditAsync(
        Guid organizationId,
        Guid assistanceItemId,
        EditAssistanceItemPaymentRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsEditAssistanceItems))
            return ServiceResult<PaymentRowDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (expectedVersion is null)
            return ServiceResult<PaymentRowDto>.Fail(409, "VERSION_CONFLICT", VersionConflictMessage);

        if (request.Fields is null || request.Fields.Count == 0)
            return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "לא נשלחו שדות לעריכה");

        foreach (var key in request.Fields.Keys)
        {
            if (!AssistanceItemEditableFields.All.Contains(key))
                return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", $"שדה אינו ניתן לעריכה: {key}");
        }

        var item = await db.AssistanceItems
            .Include(i => i.AssistanceType)
            .Include(i => i.Supplier)
            .Include(i => i.PaymentExecution)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Family)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Items)
            .FirstOrDefaultAsync(i => i.Id == assistanceItemId && i.OrganizationId == organizationId, cancellationToken);

        if (item is null)
            return ServiceResult<PaymentRowDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (item.Version != expectedVersion)
            return ServiceResult<PaymentRowDto>.Fail(409, "VERSION_CONFLICT", VersionConflictMessage);

        var hasReference = !string.IsNullOrWhiteSpace(item.ExecutionReference)
            || !string.IsNullOrWhiteSpace(item.PaymentExecution?.ExecutionReference);
        var activeExport = await db.ExportBatchItems
            .Include(x => x.ExportBatch)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId
                    && x.AssistanceItemId == assistanceItemId
                    && x.Status == ExportBatchItemStatuses.Active,
                cancellationToken);

        if (activeExport is not null)
            return ServiceResult<PaymentRowDto>.Fail(409, "EXPORT_LOCK", "לא ניתן לערוך פריט שנמצא בגליון ייצוא פעיל");

        if (hasReference)
            return ServiceResult<PaymentRowDto>.Fail(409, "INVALID_STATUS", "לא ניתן לערוך פריט לאחר הזנת אסמכתא");

        if (item.Status is AssistanceItemStatuses.Paid or AssistanceItemStatuses.Completed)
            return ServiceResult<PaymentRowDto>.Fail(409, "INVALID_STATUS", "לא ניתן לערוך פריט ששולם או שהתהליך הושלם");

        if (item.Status is not (AssistanceItemStatuses.Approved or AssistanceItemStatuses.WaitingForReference))
            return ServiceResult<PaymentRowDto>.Fail(409, "INVALID_STATUS", "לא ניתן לערוך פריט במצב זה");

        var changes = new List<AssistanceItemHistoryFieldChange>();
        var now = DateTime.UtcNow;

        // Snapshot current values for diff
        var before = new Dictionary<string, string?>
        {
            [AssistanceItemEditableFields.AssistanceTypeId] = item.AssistanceTypeId.ToString("D"),
            [AssistanceItemEditableFields.Description] = item.Description,
            [AssistanceItemEditableFields.Amount] = AssistanceItemHistoryService.FormatDecimal(item.Amount),
            [AssistanceItemEditableFields.SupplierId] = item.SupplierId?.ToString("D"),
            [AssistanceItemEditableFields.PaymentTarget] = item.PaymentTarget,
            [AssistanceItemEditableFields.Beneficiary] = item.PayeeName,
            [AssistanceItemEditableFields.PaymentMethod] = item.PaymentMethod,
            [AssistanceItemEditableFields.BankNumber] = item.TransferBankNumber,
            [AssistanceItemEditableFields.BranchNumber] = item.TransferBranchNumber,
            [AssistanceItemEditableFields.AccountNumber] = item.TransferAccountNumber,
            [AssistanceItemEditableFields.AccountHolderName] = item.AccountHolderName ?? item.PayeeName,
        };

        if (request.Fields.TryGetValue(AssistanceItemEditableFields.AssistanceTypeId, out var typeRaw))
        {
            if (!Guid.TryParse(typeRaw, out var typeId))
                return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "סוג סיוע לא חוקי");
            var assistanceType = await db.AssistanceTypes.FirstOrDefaultAsync(
                t => t.Id == typeId && t.OrganizationId == organizationId && t.Status == "active", cancellationToken);
            if (assistanceType is null)
                return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "סוג סיוע לא חוקי");
            if (item.AssistanceTypeId != typeId)
            {
                changes.Add(AssistanceItemHistoryService.CreateFieldChange(
                    AssistanceItemEditableFields.AssistanceTypeId, before[AssistanceItemEditableFields.AssistanceTypeId], typeId.ToString("D")));
                item.AssistanceTypeId = typeId;
                item.AssistanceType = assistanceType;
            }
        }

        if (request.Fields.TryGetValue(AssistanceItemEditableFields.Description, out var desc))
        {
            var next = string.IsNullOrWhiteSpace(desc) ? null : desc.Trim();
            if (!string.Equals(item.Description, next, StringComparison.Ordinal))
            {
                changes.Add(AssistanceItemHistoryService.CreateFieldChange(
                    AssistanceItemEditableFields.Description, item.Description, next));
                item.Description = next;
            }
        }

        if (request.Fields.TryGetValue(AssistanceItemEditableFields.Amount, out var amountRaw))
        {
            if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var newAmount)
                || newAmount <= 0)
            {
                return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "סכום חייב להיות גדול מאפס");
            }

            if (item.Amount != newAmount)
            {
                var reason = request.AmountAdjustmentReason?.Trim() ?? string.Empty;
                if (!AmountAdjustmentReasons.All.Contains(reason))
                    return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "סיבת עדכון סכום אינה חוקית");

                var explanation = string.IsNullOrWhiteSpace(request.AmountAdjustmentExplanation)
                    ? null
                    : request.AmountAdjustmentExplanation.Trim();
                if (AmountAdjustmentReasons.RequiresExplanation(reason))
                {
                    if (string.IsNullOrWhiteSpace(explanation) || explanation.Length < 3 || explanation.Length > 500)
                        return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "יש לציין הסבר כאשר הסיבה היא אחר");
                }
                else
                {
                    explanation = null;
                }

                changes.Add(AssistanceItemHistoryService.CreateFieldChange(
                    AssistanceItemEditableFields.Amount,
                    AssistanceItemHistoryService.FormatDecimal(item.Amount),
                    AssistanceItemHistoryService.FormatDecimal(newAmount),
                    "decimal"));
                item.PreviousPaymentAmount = item.Amount;
                item.OriginalApprovedAmount ??= item.Amount;
                item.Amount = newAmount;
                item.AmountAdjustmentReason = reason;
                item.AmountAdjustmentExplanation = explanation;
                item.AmountAdjustedByUserId = auth.UserId;
                item.AmountAdjustedAt = now;
            }
        }

        if (request.Fields.TryGetValue(AssistanceItemEditableFields.SupplierId, out var supplierRaw))
        {
            Guid? nextSupplier = null;
            if (!string.IsNullOrWhiteSpace(supplierRaw))
            {
                if (!Guid.TryParse(supplierRaw, out var sid))
                    return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "ספק לא חוקי");
                var supplier = await db.Suppliers.FirstOrDefaultAsync(
                    s => s.Id == sid && s.OrganizationId == organizationId && s.Status == "active", cancellationToken);
                if (supplier is null)
                    return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "ספק לא חוקי");
                nextSupplier = sid;
                item.Supplier = supplier;
            }
            else
            {
                item.Supplier = null;
            }

            if (item.SupplierId != nextSupplier)
            {
                changes.Add(AssistanceItemHistoryService.CreateFieldChange(
                    AssistanceItemEditableFields.SupplierId,
                    item.SupplierId?.ToString("D"),
                    nextSupplier?.ToString("D")));
                item.SupplierId = nextSupplier;
            }
        }

        if (request.Fields.TryGetValue(AssistanceItemEditableFields.PaymentTarget, out var targetRaw))
        {
            var next = targetRaw?.Trim() ?? string.Empty;
            if (!PaymentTargets.All.Contains(next))
                return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "יעד תשלום לא חוקי");
            if (!string.Equals(item.PaymentTarget, next, StringComparison.Ordinal))
            {
                changes.Add(AssistanceItemHistoryService.CreateFieldChange(
                    AssistanceItemEditableFields.PaymentTarget, item.PaymentTarget, next));
                item.PaymentTarget = next;
            }
        }

        if (request.Fields.TryGetValue(AssistanceItemEditableFields.Beneficiary, out var beneficiary))
        {
            var next = string.IsNullOrWhiteSpace(beneficiary) ? null : beneficiary.Trim();
            if (!string.Equals(item.PayeeName, next, StringComparison.Ordinal))
            {
                changes.Add(AssistanceItemHistoryService.CreateFieldChange(
                    AssistanceItemEditableFields.Beneficiary, item.PayeeName, next));
                item.PayeeName = next;
            }
        }

        if (request.Fields.TryGetValue(AssistanceItemEditableFields.PaymentMethod, out var methodRaw))
        {
            var next = methodRaw?.Trim() ?? string.Empty;
            if (!PaymentMethods.All.Contains(next))
                return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "אמצעי תשלום לא חוקי");
            if (!string.Equals(item.PaymentMethod, next, StringComparison.Ordinal))
            {
                changes.Add(AssistanceItemHistoryService.CreateFieldChange(
                    AssistanceItemEditableFields.PaymentMethod, item.PaymentMethod, next));
                item.PaymentMethod = next;
            }
        }

        ApplyStringField(request, item, AssistanceItemEditableFields.BankNumber, v => item.TransferBankNumber = v,
            () => item.TransferBankNumber, changes, maxLen: 10);
        ApplyStringField(request, item, AssistanceItemEditableFields.BranchNumber, v => item.TransferBranchNumber = v,
            () => item.TransferBranchNumber, changes, maxLen: 10);
        ApplyStringField(request, item, AssistanceItemEditableFields.AccountNumber, v => item.TransferAccountNumber = v,
            () => item.TransferAccountNumber, changes, maxLen: 34);
        ApplyStringField(request, item, AssistanceItemEditableFields.AccountHolderName, v => item.AccountHolderName = v,
            () => item.AccountHolderName, changes, maxLen: 200);

        if (changes.Count == 0)
            return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "לא בוצע שינוי בשדות");

        var fieldErrors = CommitteeItemPaymentRules.ValidateItemFields(
            item.PaymentTarget, item.PaymentMethod, item.SupplierId, item.PayeeName, item.VoucherType);
        if (fieldErrors.Count > 0)
            return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", fieldErrors[0], fieldErrors);

        if (item.PaymentTarget == PaymentTargets.Other && item.PaymentMethod == PaymentMethods.BankTransfer)
        {
            var transferErrors = CommitteeItemPaymentRules.ValidateTransferBankForOther(
                item.PaymentTarget, item.PaymentMethod, item.PayeeName,
                item.TransferBankNumber, item.TransferBranchNumber, item.TransferAccountNumber);
            if (transferErrors.Count > 0)
                return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", transferErrors[0], transferErrors);
        }

        item.Version++;
        item.UpdatedAt = now;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await historyService.AppendEventAsync(
                organizationId,
                item.Id,
                AssistanceItemHistoryEventTypes.ItemEdited,
                auth.UserId,
                null,
                null,
                null,
                request.AmountAdjustmentReason,
                changes,
                now,
                cancellationToken);

            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceItemUpdate,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "assistance_item",
                EntityId = item.Id,
                Action = "edit_payment_details",
                FieldName = string.Join(",", changes.Select(c => c.FieldKey)),
                Reason = request.AmountAdjustmentReason
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PaymentRowDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        // Reload via export service mapping path
        var refreshed = await exportBatchService.GetPaymentRowAsync(organizationId, assistanceItemId, auth, cancellationToken);
        return refreshed;
    }

    private static void ApplyStringField(
        EditAssistanceItemPaymentRequest request,
        AssistanceItem item,
        string fieldKey,
        Action<string?> setter,
        Func<string?> getter,
        List<AssistanceItemHistoryFieldChange> changes,
        int maxLen)
    {
        if (!request.Fields.TryGetValue(fieldKey, out var raw))
            return;
        var next = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (next is { Length: > 0 } && next.Length > maxLen)
            next = next[..maxLen];
        var prev = getter();
        if (string.Equals(prev, next, StringComparison.Ordinal))
            return;
        changes.Add(AssistanceItemHistoryService.CreateFieldChange(fieldKey, prev, next));
        setter(next);
    }
}
