using System.Globalization;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class ExportBatchService(
    AppDbContext db,
    IAuditService auditService,
    DocumentStorageService documentStorage,
    AssistanceItemService assistanceItemService,
    AssistanceItemHistoryService historyService)
{
    private const string MaterialReasonRequiredMessage = "יש לציין סיבה לשינוי מהותי";
    private const string VersionConflictMessage = "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.";

    public async Task<ServiceResult<PaymentRowListResponse>> ListPaymentRowsAsync(
        Guid organizationId,
        AuthorizationContext auth,
        PaymentRowListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        if (!auth.FullOrgAccess && !auth.HasGrant(PermissionKeys.PaymentsView))
            return ServiceResult<PaymentRowListResponse>.Fail(403, "FORBIDDEN", "אין הרשאה");

        query ??= new PaymentRowListQuery();
        var statuses = ResolveListStatuses(query);

        var baseQuery = db.AssistanceItems
            .Include(i => i.AssistanceType)
            .Include(i => i.Supplier)
            .Include(i => i.PaymentExecution)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Family)
            .Where(i => i.OrganizationId == organizationId && statuses.Contains(i.Status));

        if (query.MinAgeDays is > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-query.MinAgeDays.Value);
            baseQuery = baseQuery.Where(i => i.UpdatedAt < cutoff);
        }

        var items = await baseQuery
            .OrderByDescending(i => i.UpdatedAt)
            .Skip(Math.Max(0, query.Offset))
            .Take(Math.Clamp(query.Limit, 1, 200))
            .ToListAsync(cancellationToken);

        var itemIds = items.Select(i => i.Id).ToList();
        var activeExports = await db.ExportBatchItems
            .Include(x => x.ExportBatch)
            .Where(x => x.OrganizationId == organizationId
                && itemIds.Contains(x.AssistanceItemId)
                && x.Status == ExportBatchItemStatuses.Active)
            .ToListAsync(cancellationToken);
        var activeByItem = activeExports.ToDictionary(x => x.AssistanceItemId);

        var dtos = items.Select(i =>
        {
            activeByItem.TryGetValue(i.Id, out var active);
            return MapPaymentRow(i, active, auth);
        }).ToList();

        return ServiceResult<PaymentRowListResponse>.Ok(new PaymentRowListResponse
        {
            Items = dtos,
            Summary = new PaymentRowSummaryDto
            {
                Total = dtos.Count,
                Approved = dtos.Count(x => x.Status == AssistanceItemStatuses.Approved),
                WaitingForReference = dtos.Count(x => x.Status == AssistanceItemStatuses.WaitingForReference),
                Paid = dtos.Count(x => x.Status == AssistanceItemStatuses.Paid),
                Completed = dtos.Count(x => x.Status == AssistanceItemStatuses.Completed)
            }
        });
    }

    public async Task<ServiceResult<PaymentRowDto>> GetPaymentRowAsync(
        Guid organizationId,
        Guid assistanceItemId,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!auth.FullOrgAccess && !auth.HasGrant(PermissionKeys.PaymentsView))
            return ServiceResult<PaymentRowDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var item = await LoadItemAsync(organizationId, assistanceItemId, cancellationToken);
        if (item is null)
            return ServiceResult<PaymentRowDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        var active = await GetActiveExportItemAsync(organizationId, assistanceItemId, cancellationToken);
        return ServiceResult<PaymentRowDto>.Ok(MapPaymentRow(item, active, auth));
    }

    public async Task<ServiceResult<ExportBatchDto>> CreateBatchAsync(
        Guid organizationId,
        CreateExportBatchRequest request,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesCreate))
            return ServiceResult<ExportBatchDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (request.Items is null || request.Items.Count == 0)
            return ServiceResult<ExportBatchDto>.Fail(400, "VALIDATION_ERROR", "יש לבחור לפחות פריט אחד");

        var distinctIds = request.Items.Select(i => i.AssistanceItemId).Distinct().ToList();
        if (distinctIds.Count != request.Items.Count)
            return ServiceResult<ExportBatchDto>.Fail(400, "VALIDATION_ERROR", "לא ניתן לבחור את אותו פריט יותר מפעם אחת");

        var versionById = request.Items.ToDictionary(i => i.AssistanceItemId, i => i.Version);

        var items = await db.AssistanceItems
            .Include(i => i.AssistanceType)
            .Include(i => i.Supplier)
            .Include(i => i.PaymentExecution)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Family)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Items)
            .Where(i => i.OrganizationId == organizationId && distinctIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (items.Count != distinctIds.Count)
            return ServiceResult<ExportBatchDto>.Fail(404, "NOT_FOUND", "חלק מהפריטים לא נמצאו");

        var validationErrors = new List<ExportBatchRowValidationError>();
        foreach (var item in items)
        {
            if (!versionById.TryGetValue(item.Id, out var expected) || item.Version != expected)
            {
                validationErrors.Add(RowError(item, VersionConflictMessage));
                continue;
            }

            if (item.Status != AssistanceItemStatuses.Approved)
            {
                validationErrors.Add(RowError(item, "הפריט אינו בסטטוס אושר"));
                continue;
            }

            if (await db.ExportBatchItems.AnyAsync(
                    x => x.AssistanceItemId == item.Id && x.Status == ExportBatchItemStatuses.Active, cancellationToken))
            {
                validationErrors.Add(RowError(item, "הפריט כבר נמצא בגליון ייצוא פעיל"));
                continue;
            }

            foreach (var msg in ValidateAccountingCodes(item))
                validationErrors.Add(RowError(item, msg));
        }

        if (validationErrors.Count > 0)
        {
            return ServiceResult<ExportBatchDto>.FailWithStructuredDetails(
                400,
                "EXPORT_VALIDATION_ERROR",
                "לא ניתן ליצור גליון ייצוא — חסרים נתונים או הפריטים אינם כשירים",
                validationErrors);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            string batchNumber;
            var provider = db.Database.ProviderName ?? string.Empty;
            if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                var next = await db.ExportBatches.CountAsync(b => b.OrganizationId == organizationId, cancellationToken) + 1;
                batchNumber = $"EB-{next:D6}";
            }
            else
            {
                var nextCounter = await db.Database
                    .SqlQuery<int>(
                        $@"UPDATE organizations SET export_batch_counter = export_batch_counter + 1 WHERE id = {organizationId} RETURNING export_batch_counter AS ""Value""")
                    .ToListAsync(cancellationToken);
                if (nextCounter.Count == 0)
                    return ServiceResult<ExportBatchDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");
                batchNumber = $"EB-{nextCounter[0]:D6}";
            }

            var batch = new ExportBatch
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                BatchNumber = batchNumber,
                Status = ExportBatchStatuses.Open,
                CreatedByUserId = auth.UserId,
                CreatedAt = now,
                UpdatedAt = now,
                TotalItemCount = items.Count,
                ActiveItemCount = items.Count,
                CancelledItemCount = 0
            };
            db.ExportBatches.Add(batch);

            var batchItems = new List<ExportBatchItem>();
            foreach (var item in items)
            {
                var payment = item.PaymentExecution;
                if (payment is null)
                {
                    payment = new PaymentExecution
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
                }
                else
                {
                    payment.Status = PaymentExecutionStatuses.WaitingForReference;
                    payment.UpdatedAt = now;
                    payment.Version++;
                }

                item.Status = AssistanceItemStatuses.WaitingForReference;
                item.ExecutionStatus = PaymentExecutionStatuses.WaitingForReference;
                item.OriginalApprovedAmount ??= item.Amount;
                item.Version++;
                item.UpdatedAt = now;

                var decision = item.CommitteeDecision!;
                decision.Status = WorkflowHelpers.ComputeDerivedDecisionStatus(decision);
                decision.UpdatedAt = now;

                var family = decision.Family!;
                var (bank, branch, account, holder) = ExportSheetBuilder.ResolveBankDetails(item);
                var row = new ExportBatchItem
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    ExportBatchId = batch.Id,
                    PaymentExecutionId = payment.Id,
                    AssistanceItemId = item.Id,
                    ExportedAmount = item.Amount, // current payment/export amount
                    Status = ExportBatchItemStatuses.Active,
                    DecisionCode = decision.DecisionCode,
                    FamilyCode = family.FamilyCode,
                    FamilyAccountingCode = family.AccountingCode,
                    FamilyName = family.FamilyLastName,
                    AssistanceTypeName = item.AssistanceType?.Name ?? string.Empty,
                    AssistanceTypeCode = item.AssistanceType?.TypeCode ?? string.Empty,
                    OriginalApprovedAmount = item.OriginalApprovedAmount ?? item.Amount,
                    AmountAdjustmentReason = item.AmountAdjustmentReason,
                    AmountAdjustmentExplanation = AmountAdjustmentReasons.RequiresExplanation(item.AmountAdjustmentReason)
                        ? item.AmountAdjustmentExplanation
                        : null,
                    SupplierName = item.Supplier?.Name,
                    SupplierAccountingCode = item.Supplier?.AccountingCode,
                    PaymentTarget = item.PaymentTarget,
                    PaymentMethod = item.PaymentMethod,
                    PayeeName = item.PayeeName,
                    TransferBankNumber = bank,
                    TransferBranchNumber = branch,
                    TransferAccountNumber = account,
                    AccountHolderName = holder,
                    ExecutionReference = item.ExecutionReference,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.ExportBatchItems.Add(row);
                batchItems.Add(row);

                auditService.Stage(new AuditEntry
                {
                    EventCode = BusinessEventCodes.AssistanceItemStatusChange,
                    OrganizationId = organizationId,
                    ActorUserId = auth.UserId,
                    EntityType = "assistance_item",
                    EntityId = item.Id,
                    Action = AssistanceItemStatuses.WaitingForReference,
                    FieldName = "status",
                    OldValue = AssistanceItemStatuses.Approved,
                    NewValue = AssistanceItemStatuses.WaitingForReference,
                    Reason = $"export_batch:{batch.BatchNumber}"
                });

                await historyService.AppendEventAsync(
                    organizationId,
                    item.Id,
                    AssistanceItemHistoryEventTypes.ExportBatchCreated,
                    auth.UserId,
                    null,
                    "export_batch",
                    batch.Id,
                    batch.BatchNumber,
                    null,
                    now,
                    cancellationToken);
            }

            var csvBytes = ExportSheetBuilder.BuildCsv(batch, batchItems);
            await using var csvStream = new MemoryStream(csvBytes);
            var (stored, _) = await documentStorage.SaveAsync(
                organizationId, $"{batch.BatchNumber}.csv", csvStream, cancellationToken);
            batch.FileName = $"{batch.BatchNumber}.csv";
            batch.StoredFileName = stored;
            batch.ContentType = "text/csv; charset=utf-8";
            batch.FileSizeBytes = csvBytes.LongLength;
            batch.GeneratedAt = now;

            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.ExportBatchCreate,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "export_batch",
                EntityId = batch.Id,
                Action = "create",
                NewValue = batch.BatchNumber
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            batch.Items = batchItems;
            return ServiceResult<ExportBatchDto>.Ok(MapBatch(batch, auth, includeItems: true));
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<ExportBatchDto>.Fail(
                409, "DUPLICATE_ACTIVE_EXPORT", "פריט כבר כלול בגליון ייצוא פעיל");
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<ExportBatchDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }
    }

    public async Task<ServiceResult<ExportBatchListResponse>> ListBatchesAsync(
        Guid organizationId,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!auth.FullOrgAccess && !auth.HasGrant(PermissionKeys.PaymentsView))
            return ServiceResult<ExportBatchListResponse>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var batches = await db.ExportBatches
            .Where(b => b.OrganizationId == organizationId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return ServiceResult<ExportBatchListResponse>.Ok(new ExportBatchListResponse
        {
            Batches = batches.Select(b => MapBatch(b, auth, includeItems: false)).ToList()
        });
    }

    public async Task<ServiceResult<ExportBatchDto>> GetBatchAsync(
        Guid organizationId,
        Guid batchId,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!auth.FullOrgAccess && !auth.HasGrant(PermissionKeys.PaymentsView))
            return ServiceResult<ExportBatchDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var batch = await db.ExportBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken);
        if (batch is null)
            return ServiceResult<ExportBatchDto>.Fail(404, "NOT_FOUND", "גליון הייצוא לא נמצא");

        return ServiceResult<ExportBatchDto>.Ok(MapBatch(batch, auth, includeItems: true));
    }

    public async Task<ServiceResult<(Stream Content, string FileName, string ContentType)>> DownloadBatchAsync(
        Guid organizationId,
        Guid batchId,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesDownload))
            return ServiceResult<(Stream, string, string)>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var batch = await db.ExportBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken);
        if (batch is null)
            return ServiceResult<(Stream, string, string)>.Fail(404, "NOT_FOUND", "גליון הייצוא לא נמצא");

        // Re-download same batch — never create a new batch number.
        if (!string.IsNullOrWhiteSpace(batch.StoredFileName))
        {
            var opened = await documentStorage.OpenReadAsync(
                organizationId, batch.StoredFileName, batch.ContentType, cancellationToken);
            if (opened is not null)
            {
                return ServiceResult<(Stream, string, string)>.Ok((
                    opened.Value.Content,
                    batch.FileName ?? $"{batch.BatchNumber}.csv",
                    batch.ContentType ?? "text/csv; charset=utf-8"));
            }
        }

        // Regenerate from snapshot if file missing (still same batch).
        var csvBytes = ExportSheetBuilder.BuildCsv(batch, batch.Items.ToList());
        Stream stream = new MemoryStream(csvBytes);
        return ServiceResult<(Stream, string, string)>.Ok((
            stream,
            batch.FileName ?? $"{batch.BatchNumber}.csv",
            "text/csv; charset=utf-8"));
    }

    public async Task<ServiceResult<ExportBatchDto>> CancelBatchAsync(
        Guid organizationId,
        Guid batchId,
        CancelExportBatchRequest request,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesCancel))
            return ServiceResult<ExportBatchDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<ExportBatchDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var batch = await db.ExportBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken);
        if (batch is null)
            return ServiceResult<ExportBatchDto>.Fail(404, "NOT_FOUND", "גליון הייצוא לא נמצא");

        var activeItems = batch.Items.Where(i => i.Status == ExportBatchItemStatuses.Active).ToList();
        if (activeItems.Count == 0)
            return ServiceResult<ExportBatchDto>.Fail(409, "INVALID_STATUS", "אין פריטים פעילים לביטול בגליון");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            foreach (var row in activeItems)
            {
                var err = await SoftCancelExportItemAsync(row, auth, reason, now, cancellationToken);
                if (err is not null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<ExportBatchDto>.Fail(409, "INVALID_STATUS", err);
                }
            }

            RecalculateBatchCounts(batch);
            batch.UpdatedAt = now;
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.ExportBatchCancel,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "export_batch",
                EntityId = batch.Id,
                Action = "cancel",
                Reason = reason
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return ServiceResult<ExportBatchDto>.Ok(MapBatch(batch, auth, includeItems: true));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<ExportBatchDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }
    }

    public async Task<ServiceResult<ExportBatchDto>> CancelBatchItemAsync(
        Guid organizationId,
        Guid batchId,
        Guid batchItemId,
        CancelExportBatchItemRequest request,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchItemsCancel))
            return ServiceResult<ExportBatchDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<ExportBatchDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var batch = await db.ExportBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken);
        if (batch is null)
            return ServiceResult<ExportBatchDto>.Fail(404, "NOT_FOUND", "גליון הייצוא לא נמצא");

        var row = batch.Items.FirstOrDefault(i => i.Id == batchItemId);
        if (row is null)
            return ServiceResult<ExportBatchDto>.Fail(404, "NOT_FOUND", "פריט הגליון לא נמצא");
        if (row.Status != ExportBatchItemStatuses.Active)
            return ServiceResult<ExportBatchDto>.Fail(409, "INVALID_STATUS", "הפריט כבר בוטל");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var err = await SoftCancelExportItemAsync(row, auth, reason, now, cancellationToken);
            if (err is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                return ServiceResult<ExportBatchDto>.Fail(409, "INVALID_STATUS", err);
            }

            RecalculateBatchCounts(batch);
            batch.UpdatedAt = now;
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.ExportBatchItemCancel,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "export_batch_item",
                EntityId = row.Id,
                Action = "cancel",
                Reason = reason
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return ServiceResult<ExportBatchDto>.Ok(MapBatch(batch, auth, includeItems: true));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<ExportBatchDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }
    }

    public async Task<ServiceResult<PaymentRowDto>> AdjustAmountAsync(
        Guid organizationId,
        Guid assistanceItemId,
        AdjustPaymentAmountRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsEditAssistanceItems))
            return ServiceResult<PaymentRowDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (expectedVersion is null)
            return ServiceResult<PaymentRowDto>.Fail(409, "VERSION_CONFLICT", VersionConflictMessage);

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (!AmountAdjustmentReasons.All.Contains(reason))
            return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "סיבת עדכון סכום אינה חוקית");

        var explanation = string.IsNullOrWhiteSpace(request.Explanation) ? null : request.Explanation.Trim();
        if (AmountAdjustmentReasons.RequiresExplanation(reason))
        {
            if (string.IsNullOrWhiteSpace(explanation) || explanation.Length < 3 || explanation.Length > 500)
                return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "יש לציין הסבר כאשר הסיבה היא אחר");
        }
        else
        {
            explanation = null;
        }

        if (request.NewAmount <= 0)
            return ServiceResult<PaymentRowDto>.Fail(400, "VALIDATION_ERROR", "סכום חייב להיות גדול מאפס");

        var item = await LoadItemAsync(organizationId, assistanceItemId, cancellationToken);
        if (item is null)
            return ServiceResult<PaymentRowDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (item.Version != expectedVersion)
            return ServiceResult<PaymentRowDto>.Fail(409, "VERSION_CONFLICT", VersionConflictMessage);

        var hasReference = !string.IsNullOrWhiteSpace(item.ExecutionReference)
            || !string.IsNullOrWhiteSpace(item.PaymentExecution?.ExecutionReference);
        if (item.Status is not (AssistanceItemStatuses.Approved or AssistanceItemStatuses.WaitingForReference)
            || hasReference
            || item.Status is AssistanceItemStatuses.Paid or AssistanceItemStatuses.Completed)
        {
            return ServiceResult<PaymentRowDto>.Fail(409, "INVALID_STATUS", "לא ניתן לעדכן סכום במצב זה");
        }

        var activeExport = await GetActiveExportItemAsync(organizationId, assistanceItemId, cancellationToken);
        if (activeExport is not null)
            return ServiceResult<PaymentRowDto>.Fail(409, "EXPORT_LOCK", "לא ניתן לערוך פריט שנמצא בגליון ייצוא פעיל");

        var now = DateTime.UtcNow;
        var oldAmount = item.Amount;
        item.PreviousPaymentAmount = oldAmount;
        item.Amount = request.NewAmount;
        item.OriginalApprovedAmount ??= oldAmount; // never overwrite if set
        item.AmountAdjustmentReason = reason;
        item.AmountAdjustmentExplanation = explanation;
        item.AmountAdjustedByUserId = auth.UserId;
        item.AmountAdjustedAt = now;
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
                reason,
                [
                    AssistanceItemHistoryService.CreateFieldChange(
                        AssistanceItemEditableFields.Amount,
                        AssistanceItemHistoryService.FormatDecimal(oldAmount),
                        AssistanceItemHistoryService.FormatDecimal(request.NewAmount),
                        "decimal")
                ],
                now,
                cancellationToken);

            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceItemAmountAdjust,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "assistance_item",
                EntityId = item.Id,
                Action = "adjust_amount",
                FieldName = "amount",
                OldValue = oldAmount.ToString(CultureInfo.InvariantCulture),
                NewValue = request.NewAmount.ToString(CultureInfo.InvariantCulture),
                Reason = reason
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PaymentRowDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<PaymentRowDto>.Ok(MapPaymentRow(item, null, auth));
    }

    public Task<ServiceResult<AssistanceItemListDto>> EnterReferenceAsync(
        Guid organizationId,
        Guid assistanceItemId,
        EnterReferenceRequest request,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default) =>
        assistanceItemService.EnterReferenceAsync(organizationId, assistanceItemId, request, auth, cancellationToken);

    private async Task<string?> SoftCancelExportItemAsync(
        ExportBatchItem row,
        AuthorizationContext auth,
        string reason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var item = await db.AssistanceItems
            .Include(i => i.PaymentExecution)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Items)
            .FirstOrDefaultAsync(i => i.Id == row.AssistanceItemId, cancellationToken);
        if (item is null)
            return "פריט הסיוע לא נמצא";

        var hasReference = !string.IsNullOrWhiteSpace(item.ExecutionReference)
            || !string.IsNullOrWhiteSpace(item.PaymentExecution?.ExecutionReference);
        if (hasReference
            || item.Status is AssistanceItemStatuses.Paid or AssistanceItemStatuses.Completed)
        {
            return "לא ניתן לבטל ייצוא לאחר הזנת אסמכתא או תשלום";
        }

        row.Status = ExportBatchItemStatuses.Cancelled;
        row.CancelledByUserId = auth.UserId;
        row.CancelledAt = now;
        row.CancelReason = reason;
        row.UpdatedAt = now;

        var oldStatus = item.Status;
        item.Status = AssistanceItemStatuses.Approved;
        item.ExecutionStatus = PaymentExecutionStatuses.AwaitingPayment;
        item.Version++;
        item.UpdatedAt = now;
        if (item.PaymentExecution is not null)
        {
            // Keep PE history; status reflects returned-to-approved operational state.
            item.PaymentExecution.Status = PaymentExecutionStatuses.AwaitingPayment;
            item.PaymentExecution.UpdatedAt = now;
            item.PaymentExecution.Version++;
        }

        var decision = item.CommitteeDecision!;
        decision.Status = WorkflowHelpers.ComputeDerivedDecisionStatus(decision);
        decision.UpdatedAt = now;

        auditService.Stage(new AuditEntry
        {
            EventCode = BusinessEventCodes.AssistanceItemStatusChange,
            OrganizationId = item.OrganizationId,
            ActorUserId = auth.UserId,
            EntityType = "assistance_item",
            EntityId = item.Id,
            Action = AssistanceItemStatuses.Approved,
            FieldName = "status",
            OldValue = oldStatus,
            NewValue = AssistanceItemStatuses.Approved,
            Reason = reason
        });

        await historyService.AppendEventAsync(
            item.OrganizationId,
            item.Id,
            AssistanceItemHistoryEventTypes.ExportItemCancelled,
            auth.UserId,
            null,
            "export_batch_item",
            row.Id,
            reason,
            null,
            now,
            cancellationToken);

        return null;
    }

    private static void RecalculateBatchCounts(ExportBatch batch)
    {
        batch.TotalItemCount = batch.Items.Count;
        batch.ActiveItemCount = batch.Items.Count(i => i.Status == ExportBatchItemStatuses.Active);
        batch.CancelledItemCount = batch.Items.Count(i => i.Status == ExportBatchItemStatuses.Cancelled);
        batch.Status = batch.ActiveItemCount == 0
            ? ExportBatchStatuses.Cancelled
            : batch.CancelledItemCount > 0
                ? ExportBatchStatuses.PartiallyCancelled
                : ExportBatchStatuses.Open;
    }

    private static List<string> ValidateAccountingCodes(AssistanceItem item)
    {
        var errors = new List<string>();
        var family = item.CommitteeDecision?.Family;
        if (family is null || family.AccountingCode <= 0)
            errors.Add("חסר קוד משפחה בהנהלת חשבונות");

        if (string.IsNullOrWhiteSpace(item.AssistanceType?.TypeCode))
            errors.Add("חסר קוד סוג סיוע");

        if (item.PaymentTarget == PaymentTargets.Supplier
            && string.IsNullOrWhiteSpace(item.Supplier?.AccountingCode))
        {
            errors.Add("חסר קוד ספק בהנהלת חשבונות");
        }

        return errors;
    }

    private static ExportBatchRowValidationError RowError(AssistanceItem item, string message) =>
        new()
        {
            AssistanceItemId = item.Id,
            DecisionCode = item.CommitteeDecision?.DecisionCode ?? string.Empty,
            Message = message
        };

    private static HashSet<string> ResolveListStatuses(PaymentRowListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Status))
            return [query.Status];

        return query.Section switch
        {
            "finance_approved" => [AssistanceItemStatuses.Approved],
            "finance_waiting_for_reference" or "finance_awaiting_execution" =>
                [AssistanceItemStatuses.WaitingForReference],
            "finance_paid" => [AssistanceItemStatuses.Paid, AssistanceItemStatuses.Completed],
            _ =>
            [
                AssistanceItemStatuses.Approved,
                AssistanceItemStatuses.WaitingForReference,
                AssistanceItemStatuses.Paid,
                AssistanceItemStatuses.Completed
            ]
        };
    }

    private async Task<AssistanceItem?> LoadItemAsync(
        Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        await db.AssistanceItems
            .Include(i => i.AssistanceType)
            .Include(i => i.Supplier)
            .Include(i => i.PaymentExecution)
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Family)
            .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == organizationId, cancellationToken);

    private async Task<ExportBatchItem?> GetActiveExportItemAsync(
        Guid organizationId, Guid assistanceItemId, CancellationToken cancellationToken) =>
        await db.ExportBatchItems
            .Include(x => x.ExportBatch)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId
                    && x.AssistanceItemId == assistanceItemId
                    && x.Status == ExportBatchItemStatuses.Active,
                cancellationToken);

    private static PaymentRowDto MapPaymentRow(
        AssistanceItem i,
        ExportBatchItem? activeExport,
        AuthorizationContext auth)
    {
        var decision = i.CommitteeDecision!;
        var family = decision.Family!;
        var hasActive = activeExport is not null;
        return new PaymentRowDto
        {
            AssistanceItemId = i.Id,
            PaymentExecutionId = i.PaymentExecution?.Id,
            CommitteeDecisionId = decision.Id,
            DecisionCode = decision.DecisionCode,
            FamilyId = family.Id,
            FamilyCode = family.FamilyCode,
            FamilyAccountingCode = family.AccountingCode,
            FamilyLastName = family.FamilyLastName,
            AssistanceTypeId = i.AssistanceTypeId,
            AssistanceTypeName = i.AssistanceType?.Name ?? string.Empty,
            AssistanceTypeCode = i.AssistanceType?.TypeCode ?? string.Empty,
            Amount = i.Amount,
            OriginalApprovedAmount = i.OriginalApprovedAmount,
            PreviousPaymentAmount = i.PreviousPaymentAmount,
            AmountAdjustmentReason = i.AmountAdjustmentReason,
            AmountAdjustmentExplanation = i.AmountAdjustmentExplanation,
            PaymentTarget = i.PaymentTarget,
            PaymentMethod = i.PaymentMethod,
            Description = i.Description,
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
            Status = i.Status,
            ExecutionReference = i.ExecutionReference ?? i.PaymentExecution?.ExecutionReference,
            ActiveExportBatchId = activeExport?.ExportBatchId,
            ActiveExportBatchNumber = activeExport?.ExportBatch?.BatchNumber,
            ActiveExportBatchItemId = activeExport?.Id,
            EligibleForExport = WorkflowHelpers.IsEligibleForExport(i, hasActive, auth),
            AvailableActions = WorkflowHelpers.AvailablePaymentRowActions(i, activeExport, auth),
            Version = i.Version,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt
        };
    }

    private static ExportBatchDto MapBatch(ExportBatch batch, AuthorizationContext auth, bool includeItems) =>
        new()
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            Status = batch.Status,
            CreatedByUserId = batch.CreatedByUserId,
            CreatedAt = batch.CreatedAt,
            UpdatedAt = batch.UpdatedAt,
            FileName = batch.FileName,
            ContentType = batch.ContentType,
            FileSizeBytes = batch.FileSizeBytes,
            GeneratedAt = batch.GeneratedAt,
            TotalItemCount = batch.TotalItemCount,
            ActiveItemCount = batch.ActiveItemCount,
            CancelledItemCount = batch.CancelledItemCount,
            AvailableActions = WorkflowHelpers.AvailableExportBatchActions(batch, auth),
            Items = includeItems
                ? batch.Items.Select(MapBatchItem).ToList()
                : null
        };

    private static ExportBatchItemDto MapBatchItem(ExportBatchItem i) =>
        new()
        {
            Id = i.Id,
            AssistanceItemId = i.AssistanceItemId,
            PaymentExecutionId = i.PaymentExecutionId,
            ExportedAmount = i.ExportedAmount,
            Status = i.Status,
            CancelReason = i.CancelReason,
            CancelledAt = i.CancelledAt,
            DecisionCode = i.DecisionCode,
            FamilyCode = i.FamilyCode,
            FamilyAccountingCode = i.FamilyAccountingCode,
            FamilyName = i.FamilyName,
            AssistanceTypeName = i.AssistanceTypeName,
            AssistanceTypeCode = i.AssistanceTypeCode,
            OriginalApprovedAmount = i.OriginalApprovedAmount,
            AmountAdjustmentReason = i.AmountAdjustmentReason,
            AmountAdjustmentExplanation = i.AmountAdjustmentExplanation,
            SupplierName = i.SupplierName,
            SupplierAccountingCode = i.SupplierAccountingCode,
            PaymentTarget = i.PaymentTarget,
            PaymentMethod = i.PaymentMethod,
            PayeeName = i.PayeeName,
            ExecutionReference = i.ExecutionReference
        };
}
