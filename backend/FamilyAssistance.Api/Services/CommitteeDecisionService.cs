using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class CommitteeDecisionService(
    AppDbContext db,
    IAuditService auditService)
{
    private const int MaxItems = 20;
    private const string MaterialReasonRequiredMessage = "יש לציין סיבה לשינוי מהותי";

    public async Task<ServiceResult<CommitteeDecisionListResponse>> ListAsync(
        Guid organizationId,
        AuthorizationContext auth,
        CommitteeDecisionListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new CommitteeDecisionListQuery();

        var baseQuery = ScopeEvaluator.ApplyCommitteeListScope(
            db.CommitteeDecisions
                .Include(d => d.Family)
                .Include(d => d.CreatedByUser)
                .Include(d => d.Items)
                    .ThenInclude(i => i.AssistanceType)
                .Include(d => d.Items)
                    .ThenInclude(i => i.Supplier)
                .Where(d => d.OrganizationId == organizationId),
            auth,
            PermissionKeys.CommitteeDecisionsView);

        baseQuery = ApplyDecisionListFilters(baseQuery, query, auth);

        var decisions = await baseQuery
            .OrderByDescending(d => d.CreatedAt)
            .Skip(Math.Max(0, query.Offset))
            .Take(Math.Clamp(query.Limit, 1, 200))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.WorkflowPhase))
        {
            decisions = decisions
                .Where(d => WorkflowHelpers.ComputeWorkflowPhase(d) == query.WorkflowPhase)
                .ToList();
        }

        var decisionIds = decisions.Select(d => d.Id).ToList();
        var paymentsByItem = await LoadPaymentsByItemIdAsync(decisionIds, cancellationToken);

        var dtos = decisions.Select(d => MapDecision(d, auth, paymentsByItem)).ToList();
        return ServiceResult<CommitteeDecisionListResponse>.Ok(new CommitteeDecisionListResponse
        {
            Summary = new CommitteeDecisionSummaryDto
            {
                Total = dtos.Count,
                Draft = dtos.Count(d => d.Status == CommitteeDecisionStatuses.Draft),
                Submitted = dtos.Count(d => d.Status == CommitteeDecisionStatuses.Submitted),
                Approved = dtos.Count(d => d.Status is CommitteeDecisionStatuses.Approved
                    or CommitteeDecisionStatuses.PartiallyPaid or CommitteeDecisionStatuses.FullyPaid)
            },
            Decisions = dtos
        });
    }

    public async Task<ServiceResult<CommitteeDecisionDto>> GetAsync(
        Guid organizationId,
        Guid id,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var decision = await LoadDecisionAsync(organizationId, id, cancellationToken);
        if (decision is null)
            return ServiceResult<CommitteeDecisionDto>.Fail(404, "NOT_FOUND", "ההחלטה לא נמצאה");

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision, PermissionKeys.CommitteeDecisionsView))
            return ServiceResult<CommitteeDecisionDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var paymentsByItem = await LoadPaymentsByItemIdAsync([decision.Id], cancellationToken);
        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision, auth, paymentsByItem));
    }

    public async Task<ServiceResult<CommitteeDecisionDto>> CreateAsync(
        Guid organizationId,
        CreateCommitteeDecisionRequest request,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var family = await db.Families.FirstOrDefaultAsync(
            f => f.Id == request.FamilyId && f.OrganizationId == organizationId, cancellationToken);
        if (family is null)
            return ServiceResult<CommitteeDecisionDto>.Fail(404, "NOT_FOUND", "המשפחה לא נמצאה");
        if (family.Status != "active")
            return ServiceResult<CommitteeDecisionDto>.Fail(409, "FAMILY_INACTIVE", "המשפחה אינה פעילה");

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, family, PermissionKeys.CommitteeDecisionsCreate))
            return ServiceResult<CommitteeDecisionDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var nextCounter = await db.Database
                .SqlQuery<int>(
                    $@"UPDATE organizations SET decision_code_counter = decision_code_counter + 1 WHERE id = {organizationId} RETURNING decision_code_counter AS ""Value""")
                .ToListAsync(cancellationToken);

            if (nextCounter.Count == 0)
                return ServiceResult<CommitteeDecisionDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");

            var now = DateTime.UtcNow;
            var decision = new CommitteeDecision
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                DecisionCode = $"D-{nextCounter[0]:D6}",
                FamilyId = family.Id,
                MeetingDate = request.MeetingDate,
                Summary = NormalizeOptional(request.Summary),
                Status = CommitteeDecisionStatuses.Draft,
                CreatedByUserId = auth.UserId,
                TotalAmount = 0,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.CommitteeDecisions.Add(decision);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.CommitteeDecisionCreate,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "committee_decision",
                EntityId = decision.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new { decision.DecisionCode, decision.FamilyId })
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            decision.Family = family;
            decision.CreatedByUser = await db.Users.FindAsync([auth.UserId], cancellationToken);
            return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision, auth));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<CommitteeDecisionDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }
    }

    public async Task<ServiceResult<CommitteeDecisionDto>> UpdateDraftAsync(
        Guid organizationId,
        Guid id,
        UpdateCommitteeDecisionRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<CommitteeDecisionDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var decision = await LoadDecisionAsync(organizationId, id, cancellationToken);
        if (decision is null)
            return ServiceResult<CommitteeDecisionDto>.Fail(404, "NOT_FOUND", "ההחלטה לא נמצאה");

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision, PermissionKeys.CommitteeDecisionsEditDraft))
            return ServiceResult<CommitteeDecisionDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (!WorkflowHelpers.CanActAsDecisionOwner(decision, auth))
            return ServiceResult<CommitteeDecisionDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (!CommitteeDecisionStatuses.EditableHeader.Contains(decision.Status))
            return ServiceResult<CommitteeDecisionDto>.Fail(409, "INVALID_STATUS", "ההחלטה אינה בעריכה");

        if (decision.Version != expectedVersion)
            return ServiceResult<CommitteeDecisionDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var changes = new List<(string Field, string? Old, string? New)>();

        if (request.MeetingDate is not null && request.MeetingDate != decision.MeetingDate)
        {
            changes.Add(("meeting_date", decision.MeetingDate.ToString(), request.MeetingDate.Value.ToString()));
            decision.MeetingDate = request.MeetingDate.Value;
        }

        if (request.Summary is not null)
        {
            var newSummary = NormalizeOptional(request.Summary);
            if (newSummary is not null && newSummary.Length > 2000)
                return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", "סיכום חייב להיות עד 2000 תווים");
            if (newSummary != decision.Summary)
            {
                changes.Add(("summary", decision.Summary, newSummary));
                decision.Summary = newSummary;
            }
        }

        if (changes.Count == 0)
            return ServiceResult<CommitteeDecisionDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        decision.Version++;
        decision.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var change in changes)
            {
                auditService.Stage(new AuditEntry
                {
                    EventCode = BusinessEventCodes.CommitteeDecisionStatusChange,
                    OrganizationId = organizationId,
                    ActorUserId = auth.UserId,
                    EntityType = "committee_decision",
                    EntityId = decision.Id,
                    Action = "update",
                    FieldName = change.Field,
                    OldValue = change.Old,
                    NewValue = change.New
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<CommitteeDecisionDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision, auth));
    }

    public async Task<ServiceResult<CommitteeDecisionDto>> SubmitAsync(
        Guid organizationId,
        Guid id,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var decision = await LoadDecisionForTransitionAsync(organizationId, id, expectedVersion, auth,
            PermissionKeys.CommitteeDecisionsSubmit,
            [CommitteeDecisionStatuses.Draft, CommitteeDecisionStatuses.ReturnedForRevision],
            cancellationToken);
        if (!decision.IsSuccess)
            return ServiceResult<CommitteeDecisionDto>.Fail(decision.StatusCode, decision.Code, decision.Error);

        var entity = decision.Value!;
        if (!WorkflowHelpers.CanActAsDecisionOwner(entity, auth))
            return ServiceResult<CommitteeDecisionDto>.Fail(403, "FORBIDDEN", "אין הרשאה");
        if (entity.Items.Count == 0)
            return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", "יש להוסיף לפחות פריט סיוע אחד");

        foreach (var item in entity.Items)
        {
            var itemErrors = CommitteeItemPaymentRules.ValidateItemFields(
                item.PaymentTarget, item.PaymentMethod, item.SupplierId, item.PayeeName, item.VoucherType);
            if (itemErrors.Count > 0)
                return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", itemErrors[0], itemErrors);

            Supplier? supplier = item.Supplier;
            if (item.SupplierId is not null && supplier is null)
            {
                supplier = await db.Suppliers.FirstOrDefaultAsync(
                    s => s.Id == item.SupplierId && s.OrganizationId == organizationId,
                    cancellationToken);
            }

            var bankErrors = CommitteeItemPaymentRules.ValidateBankForTransfer(
                item.PaymentTarget, item.PaymentMethod, entity.Family!, supplier);
            if (bankErrors.Count > 0)
                return ServiceResult<CommitteeDecisionDto>.Fail(400, "INCOMPLETE_BANK_DETAILS", bankErrors[0], bankErrors);

            var transferErrors = CommitteeItemPaymentRules.ValidateTransferBankForOther(
                item.PaymentTarget, item.PaymentMethod, item.PayeeName,
                item.TransferBankNumber, item.TransferBranchNumber, item.TransferAccountNumber);
            if (transferErrors.Count > 0)
                return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", transferErrors[0], transferErrors);
        }

        return await TransitionStatusAsync(entity, CommitteeDecisionStatuses.Submitted, auth,
            d =>
            {
                d.SubmittedAt = DateTime.UtcNow;
                foreach (var item in d.Items)
                {
                    item.Status = AssistanceItemStatuses.Submitted;
                    item.UpdatedAt = DateTime.UtcNow;
                }
            }, null, cancellationToken);
    }

    public Task<ServiceResult<CommitteeDecisionDto>> ApproveAsync(
        Guid organizationId,
        Guid id,
        ApproveCommitteeDecisionRequest? request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<CommitteeDecisionDto>.Fail(409, "DEPRECATED_ENDPOINT",
            "אישור מתבצע ברמת פריט הסיוע. יש להשתמש ב־POST /api/v1/org/assistance-items/{id}/approve"));
    }

    public Task<ServiceResult<CommitteeDecisionDto>> RejectAsync(
        Guid organizationId,
        Guid id,
        RejectCommitteeDecisionRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<CommitteeDecisionDto>.Fail(409, "DEPRECATED_ENDPOINT",
            "דחייה/החזרה מתבצעת ברמת פריט הסיוע. יש להשתמש ב־POST /api/v1/org/assistance-items/{id}/reject או /return"));
    }

    public Task<ServiceResult<CommitteeDecisionDto>> SuspendAsync(
        Guid organizationId,
        Guid id,
        StatusTransitionRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<CommitteeDecisionDto>.Fail(409, "DEPRECATED_ENDPOINT",
            "השהיה מתבצעת ברמת פריט הסיוע. יש להשתמש ב־POST /api/v1/org/assistance-items/{id}/suspend"));
    }

    public Task<ServiceResult<CommitteeDecisionDto>> ResumeAsync(
        Guid organizationId,
        Guid id,
        ResumeCommitteeDecisionRequest? request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<CommitteeDecisionDto>.Fail(409, "DEPRECATED_ENDPOINT",
            "חידוש ברמת החלטה אינו נתמך. יש לטפל בפריטי הסיוע בנפרד"));
    }
    public async Task<ServiceResult<CommitteeDecisionDto>> CancelAsync(
        Guid organizationId,
        Guid id,
        StatusTransitionRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var allowed = new HashSet<string>
        {
            CommitteeDecisionStatuses.Draft,
            CommitteeDecisionStatuses.Submitted,
            CommitteeDecisionStatuses.ReturnedForRevision,
            CommitteeDecisionStatuses.Approved
        };

        var decisionResult = await LoadDecisionForTransitionAsync(organizationId, id, expectedVersion, auth,
            PermissionKeys.CommitteeDecisionsCancel, allowed, cancellationToken);
        if (!decisionResult.IsSuccess)
            return ServiceResult<CommitteeDecisionDto>.Fail(decisionResult.StatusCode, decisionResult.Code, decisionResult.Error);

        var decision = decisionResult.Value!;
        decision.CancelReason = reason;
        decision.CancelledAt = DateTime.UtcNow;

        return await TransitionStatusAsync(decision, CommitteeDecisionStatuses.Cancelled, auth, _ => { }, reason, cancellationToken);
    }

    public async Task<ServiceResult<bool>> DeleteDraftAsync(
        Guid organizationId,
        Guid id,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<bool>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var decision = await LoadDecisionAsync(organizationId, id, cancellationToken);
        if (decision is null)
            return ServiceResult<bool>.Fail(404, "NOT_FOUND", "ההחלטה לא נמצאה");

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision, PermissionKeys.CommitteeDecisionsEditDraft))
            return ServiceResult<bool>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (decision.Status != CommitteeDecisionStatuses.Draft)
            return ServiceResult<bool>.Fail(409, "INVALID_STATUS", "ההחלטה אינה בעריכה");

        if (decision.Version != expectedVersion)
            return ServiceResult<bool>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.CommitteeDecisionDelete,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "committee_decision",
                EntityId = decision.Id,
                Action = "delete",
            });

            db.AssistanceItems.RemoveRange(decision.Items);
            db.CommitteeDecisions.Remove(decision);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<bool>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<(AssistanceItemDto Item, int DecisionVersion)>> AddItemAsync(
        Guid organizationId,
        Guid decisionId,
        CreateAssistanceItemRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var decision = await LoadDecisionAsync(organizationId, decisionId, cancellationToken);
        if (decision is null)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(404, "NOT_FOUND", "ההחלטה לא נמצאה");

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision, PermissionKeys.AssistanceItemsCreate))
            return ServiceResult<(AssistanceItemDto, int)>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (decision.Version != expectedVersion)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        if (!CommitteeDecisionStatuses.EditableItems.Contains(decision.Status))
            return ServiceResult<(AssistanceItemDto, int)>.Fail(409, "INVALID_STATUS", "לא ניתן לערוך פריטים במצב זה");

        if (!WorkflowHelpers.CanActAsDecisionOwner(decision, auth))
            return ServiceResult<(AssistanceItemDto, int)>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (decision.Items.Count >= MaxItems)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(400, "VALIDATION_ERROR", "ניתן להוסיף עד 20 פריטי סיוע");

        var errors = ValidateCreateItemRequest(request);
        if (errors.Count > 0)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var assistanceType = await db.AssistanceTypes.FirstOrDefaultAsync(
            t => t.Id == request.AssistanceTypeId && t.OrganizationId == organizationId && t.Status == "active",
            cancellationToken);
        if (assistanceType is null)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(400, "VALIDATION_ERROR", "סוג סיוע לא חוקי");

        Supplier? supplier = null;
        if (request.SupplierId is not null)
        {
            supplier = await db.Suppliers.FirstOrDefaultAsync(
                s => s.Id == request.SupplierId && s.OrganizationId == organizationId && s.Status == "active",
                cancellationToken);
            if (supplier is null)
                return ServiceResult<(AssistanceItemDto, int)>.Fail(400, "VALIDATION_ERROR", "ספק לא חוקי");
        }

        var bankErrors = CommitteeItemPaymentRules.ValidateBankForTransfer(
            request.PaymentTarget.Trim(),
            request.PaymentMethod.Trim(),
            decision.Family!,
            supplier);
        if (bankErrors.Count > 0)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(400, "INCOMPLETE_BANK_DETAILS", bankErrors[0], bankErrors);

        var transferErrors = CommitteeItemPaymentRules.ValidateTransferBankForOther(
            request.PaymentTarget.Trim(),
            request.PaymentMethod.Trim(),
            request.PayeeName,
            request.TransferBankNumber,
            request.TransferBranchNumber,
            request.TransferAccountNumber);
        if (transferErrors.Count > 0)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(400, "VALIDATION_ERROR", transferErrors[0], transferErrors);

        var now = DateTime.UtcNow;
        var lineNumber = decision.Items.Count == 0
            ? 1
            : decision.Items.Max(i => i.LineNumber) + 1;

        var item = new AssistanceItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CommitteeDecisionId = decision.Id,
            LineNumber = lineNumber,
            AssistanceTypeId = request.AssistanceTypeId,
            Description = NormalizeOptional(request.Description),
            Amount = request.Amount,
            PaymentTarget = request.PaymentTarget.Trim(),
            PaymentMethod = request.PaymentMethod.Trim(),
            SupplierId = request.SupplierId,
            PayeeName = NormalizeOptional(request.PayeeName),
            TransferBankNumber = NormalizeOptional(request.TransferBankNumber),
            TransferBranchNumber = NormalizeOptional(request.TransferBranchNumber),
            TransferAccountNumber = NormalizeOptional(request.TransferAccountNumber),
            VoucherType = NormalizeOptional(request.VoucherType),
            IsUrgent = request.IsUrgent,
            Status = AssistanceItemStatuses.Draft,
            ExecutionStatus = PaymentExecutionStatuses.AwaitingPayment,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        CommitteeItemPaymentRules.SyncTransferBankStorage(item);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.AssistanceItems.Add(item);
            decision.TotalAmount += item.Amount;
            decision.Version++;
            decision.UpdatedAt = now;

            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceItemCreate,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "assistance_item",
                EntityId = item.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new { item.LineNumber, item.Amount })
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<(AssistanceItemDto, int)>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        item.AssistanceType = assistanceType;
        if (supplier is not null)
            item.Supplier = supplier;

        return ServiceResult<(AssistanceItemDto Item, int DecisionVersion)>.Ok((MapItem(item, decision, auth), decision.Version));
    }

    public async Task<ServiceResult<AssistanceItemDto>> UpdateItemAsync(
        Guid organizationId,
        Guid decisionId,
        Guid itemId,
        UpdateAssistanceItemRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<AssistanceItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var decision = await LoadDecisionAsync(organizationId, decisionId, cancellationToken);
        if (decision is null)
            return ServiceResult<AssistanceItemDto>.Fail(404, "NOT_FOUND", "ההחלטה לא נמצאה");

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision, PermissionKeys.AssistanceItemsEdit))
            return ServiceResult<AssistanceItemDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var item = decision.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return ServiceResult<AssistanceItemDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        // Phase 14: item-level edit for draft/returned rows (sibling independence).
        if (item.Status is not (AssistanceItemStatuses.Draft or AssistanceItemStatuses.Returned)
            && !CommitteeDecisionStatuses.EditableItems.Contains(decision.Status))
            return ServiceResult<AssistanceItemDto>.Fail(409, "INVALID_STATUS", "לא ניתן לערוך פריטים במצב זה");

        if (item.Status is AssistanceItemStatuses.Draft or AssistanceItemStatuses.Returned
            && !WorkflowHelpers.CanActAsDecisionOwner(decision, auth))
            return ServiceResult<AssistanceItemDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (item.Version != expectedVersion)
            return ServiceResult<AssistanceItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var oldAmount = item.Amount;
        ApplyItemUpdates(request, item, out var errors);
        if (errors.Count > 0)
            return ServiceResult<AssistanceItemDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var validationErrors = CommitteeItemPaymentRules.ValidateItemFields(
            item.PaymentTarget, item.PaymentMethod, item.SupplierId, item.PayeeName, item.VoucherType);
        if (validationErrors.Count > 0)
            return ServiceResult<AssistanceItemDto>.Fail(400, "VALIDATION_ERROR", validationErrors[0], validationErrors);

        if (request.AssistanceTypeId is not null)
        {
            var assistanceType = await db.AssistanceTypes.FirstOrDefaultAsync(
                t => t.Id == request.AssistanceTypeId && t.OrganizationId == organizationId && t.Status == "active",
                cancellationToken);
            if (assistanceType is null)
                return ServiceResult<AssistanceItemDto>.Fail(400, "VALIDATION_ERROR", "סוג סיוע לא חוקי");
            item.AssistanceType = assistanceType;
        }

        Supplier? supplierForBank = item.Supplier;
        if (request.SupplierId is not null || request.ClearSupplierId)
        {
            if (request.ClearSupplierId)
            {
                item.Supplier = null;
                supplierForBank = null;
            }
            else if (request.SupplierId is not null)
            {
                supplierForBank = await db.Suppliers.FirstOrDefaultAsync(
                    s => s.Id == request.SupplierId && s.OrganizationId == organizationId && s.Status == "active",
                    cancellationToken);
                if (supplierForBank is null)
                    return ServiceResult<AssistanceItemDto>.Fail(400, "VALIDATION_ERROR", "ספק לא חוקי");
                item.Supplier = supplierForBank;
            }
        }
        else if (item.SupplierId is not null && supplierForBank is null)
        {
            supplierForBank = await db.Suppliers.FirstOrDefaultAsync(
                s => s.Id == item.SupplierId && s.OrganizationId == organizationId,
                cancellationToken);
        }

        var bankErrors = CommitteeItemPaymentRules.ValidateBankForTransfer(
            item.PaymentTarget, item.PaymentMethod, decision.Family!, supplierForBank);
        if (bankErrors.Count > 0)
            return ServiceResult<AssistanceItemDto>.Fail(400, "INCOMPLETE_BANK_DETAILS", bankErrors[0], bankErrors);

        var transferErrors = CommitteeItemPaymentRules.ValidateTransferBankForOther(
            item.PaymentTarget, item.PaymentMethod, item.PayeeName,
            item.TransferBankNumber, item.TransferBranchNumber, item.TransferAccountNumber);
        if (transferErrors.Count > 0)
            return ServiceResult<AssistanceItemDto>.Fail(400, "VALIDATION_ERROR", transferErrors[0], transferErrors);

        decision.TotalAmount = decision.TotalAmount - oldAmount + item.Amount;
        item.Version++;
        item.UpdatedAt = DateTime.UtcNow;
        decision.Version++;
        decision.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceItemUpdate,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "assistance_item",
                EntityId = item.Id,
                Action = "update",
                FieldName = "amount",
                OldValue = oldAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                NewValue = item.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<AssistanceItemDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<AssistanceItemDto>.Ok(MapItem(item, decision, auth));
    }

    public async Task<ServiceResult<CommitteeDecisionDto>> RemoveItemAsync(
        Guid organizationId,
        Guid decisionId,
        Guid itemId,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<CommitteeDecisionDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var decision = await LoadDecisionAsync(organizationId, decisionId, cancellationToken);
        if (decision is null)
            return ServiceResult<CommitteeDecisionDto>.Fail(404, "NOT_FOUND", "ההחלטה לא נמצאה");

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision, PermissionKeys.AssistanceItemsRemoveDraft))
            return ServiceResult<CommitteeDecisionDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (!CommitteeDecisionStatuses.EditableItems.Contains(decision.Status))
            return ServiceResult<CommitteeDecisionDto>.Fail(409, "INVALID_STATUS", "לא ניתן להסיר פריטים במצב זה");

        var item = decision.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return ServiceResult<CommitteeDecisionDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (decision.Version != expectedVersion)
            return ServiceResult<CommitteeDecisionDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        decision.TotalAmount -= item.Amount;
        decision.Version++;
        decision.UpdatedAt = DateTime.UtcNow;
        db.AssistanceItems.Remove(item);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<CommitteeDecisionDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision, auth));
    }

    private async Task<ServiceResult<CommitteeDecision>> LoadDecisionForTransitionAsync(
        Guid organizationId,
        Guid id,
        int? expectedVersion,
        AuthorizationContext auth,
        string permissionKey,
        HashSet<string> allowedStatuses,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is null)
            return ServiceResult<CommitteeDecision>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var decision = await LoadDecisionAsync(organizationId, id, cancellationToken);
        if (decision is null)
            return ServiceResult<CommitteeDecision>.Fail(404, "NOT_FOUND", "ההחלטה לא נמצאה");

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision, permissionKey))
            return ServiceResult<CommitteeDecision>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (!allowedStatuses.Contains(decision.Status))
            return ServiceResult<CommitteeDecision>.Fail(409, "INVALID_STATUS", "מעבר סטטוס לא חוקי");

        if (decision.Version != expectedVersion)
            return ServiceResult<CommitteeDecision>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        return ServiceResult<CommitteeDecision>.Ok(decision);
    }

    private async Task<ServiceResult<CommitteeDecisionDto>> TransitionStatusAsync(
        CommitteeDecision decision,
        string newStatus,
        AuthorizationContext auth,
        Action<CommitteeDecision> beforeSave,
        string? reason,
        CancellationToken cancellationToken)
    {
        var oldStatus = decision.Status;
        decision.Status = newStatus;
        decision.Version++;
        decision.UpdatedAt = DateTime.UtcNow;
        beforeSave(decision);

        var action = newStatus switch
        {
            CommitteeDecisionStatuses.Submitted => "submit",
            _ => "status_change"
        };

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.CommitteeDecisionStatusChange,
                OrganizationId = decision.OrganizationId,
                ActorUserId = auth.UserId,
                EntityType = "committee_decision",
                EntityId = decision.Id,
                Action = action,
                FieldName = "status",
                OldValue = oldStatus,
                NewValue = newStatus,
                Reason = reason
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<CommitteeDecisionDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision, auth));
    }

    private async Task<CommitteeDecision?> LoadDecisionAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken) =>
        await db.CommitteeDecisions
            .Include(d => d.Family)
            .Include(d => d.CreatedByUser)
            .Include(d => d.Items)
                .ThenInclude(i => i.AssistanceType)
            .Include(d => d.Items)
                .ThenInclude(i => i.Supplier)
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == organizationId, cancellationToken);

    private static List<string> ValidateCreateItemRequest(CreateAssistanceItemRequest request)
    {
        var errors = new List<string>();
        if (request.Amount <= 0 || request.Amount > 1_000_000)
            errors.Add("סכום חייב להיות בין 0 ל-1,000,000");
        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 500)
            errors.Add("תיאור חייב להיות עד 500 תווים");

        errors.AddRange(CommitteeItemPaymentRules.ValidateItemFields(
            request.PaymentTarget?.Trim() ?? string.Empty,
            request.PaymentMethod?.Trim() ?? string.Empty,
            request.SupplierId,
            request.PayeeName,
            request.VoucherType));
        return errors;
    }

    private static void ApplyItemUpdates(UpdateAssistanceItemRequest request, AssistanceItem item, out List<string> errors)
    {
        errors = new List<string>();

        if (request.AssistanceTypeId is not null)
            item.AssistanceTypeId = request.AssistanceTypeId.Value;

        if (request.Description is not null)
            item.Description = NormalizeOptional(request.Description);

        if (request.Amount is not null)
        {
            if (request.Amount <= 0 || request.Amount > 1_000_000)
                errors.Add("סכום חייב להיות בין 0 ל-1,000,000");
            else
                item.Amount = request.Amount.Value;
        }

        if (request.PaymentTarget is not null)
            item.PaymentTarget = request.PaymentTarget.Trim();
        if (request.PaymentMethod is not null)
            item.PaymentMethod = request.PaymentMethod.Trim();

        if (request.ClearSupplierId)
            item.SupplierId = null;
        else if (request.SupplierId is not null)
            item.SupplierId = request.SupplierId;

        if (request.PayeeName is not null)
            item.PayeeName = NormalizeOptional(request.PayeeName);
        CommitteeItemPaymentRules.ApplyTransferBankFromRequest(
            item,
            request.TransferBankNumber,
            request.TransferBranchNumber,
            request.TransferAccountNumber,
            request.ClearTransferBank);
        if (request.VoucherType is not null)
            item.VoucherType = NormalizeOptional(request.VoucherType);

        if (request.IsUrgent is not null)
            item.IsUrgent = request.IsUrgent.Value;

        CommitteeItemPaymentRules.SyncTransferBankStorage(item);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static IQueryable<CommitteeDecision> ApplyDecisionListFilters(
        IQueryable<CommitteeDecision> query,
        CommitteeDecisionListQuery listQuery,
        AuthorizationContext auth)
    {
        if (listQuery.FamilyId is not null)
            query = query.Where(d => d.FamilyId == listQuery.FamilyId);

        if (!string.IsNullOrWhiteSpace(listQuery.Status))
            query = query.Where(d => d.Status == listQuery.Status);

        if (listQuery.Ownership == "mine")
            query = WorkflowHelpers.ApplyOwnershipMine(query, auth.UserId);

        if (!string.IsNullOrWhiteSpace(listQuery.Section))
        {
            if (WorkflowSectionRegistry.IsDecisionSectionOrgScoped(listQuery.Section))
            {
                var grant = auth.GetGrant(PermissionKeys.CommitteeDecisionsView);
                if (!auth.FullOrgAccess && grant?.Scope != PermissionScopes.Organization)
                    query = query.Where(_ => false);
            }
            else if (listQuery.Section.StartsWith("my_", StringComparison.Ordinal))
            {
                query = WorkflowHelpers.ApplyOwnershipMine(query, auth.UserId);
            }

            query = listQuery.Section switch
            {
                "my_drafts" => query.Where(d => d.Status == CommitteeDecisionStatuses.Draft),
                "my_returned_for_revision" => query.Where(d => d.Status == CommitteeDecisionStatuses.ReturnedForRevision),
                "my_waiting_manager_approval" => query.Where(d => d.Status == CommitteeDecisionStatuses.Submitted),
                "my_suspended" => query.Where(d => d.Status == CommitteeDecisionStatuses.Suspended),
                "my_in_finance_execution" => query.Where(d =>
                    d.Status == CommitteeDecisionStatuses.Approved || d.Status == CommitteeDecisionStatuses.PartiallyPaid),
                "my_paid_completed" => query.Where(d => d.Status == CommitteeDecisionStatuses.FullyPaid),
                "my_rejected" => query.Where(d =>
                    d.Status == CommitteeDecisionStatuses.Rejected || d.Status == CommitteeDecisionStatuses.Cancelled),
                "waiting_my_approval" => query.Where(d => d.Status == CommitteeDecisionStatuses.Submitted),
                "approved" => query.Where(d =>
                    d.Status == CommitteeDecisionStatuses.Approved || d.Status == CommitteeDecisionStatuses.PartiallyPaid),
                "manager_rejected" => query.Where(d => d.Status == CommitteeDecisionStatuses.Rejected),
                "manager_returned" => query.Where(d => d.Status == CommitteeDecisionStatuses.ReturnedForRevision),
                "manager_suspended" => query.Where(d => d.Status == CommitteeDecisionStatuses.Suspended),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(listQuery.Q))
        {
            var term = listQuery.Q.Trim().ToLowerInvariant();
            query = query.Where(d =>
                d.DecisionCode.ToLower().Contains(term)
                || d.Family!.FamilyCode.ToLower().Contains(term)
                || d.Family.FamilyLastName.ToLower().Contains(term));
        }

        if (listQuery.MinAgeDays is > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-listQuery.MinAgeDays.Value);
            query = query.Where(d =>
                (d.Status != CommitteeDecisionStatuses.Submitted
                    || (d.SubmittedAt != null && d.SubmittedAt < cutoff))
                && (d.Status != CommitteeDecisionStatuses.Suspended
                    || (d.SuspendedAt != null && d.SuspendedAt < cutoff)));
        }

        return query;
    }

    private async Task<Dictionary<Guid, PaymentExecution>> LoadPaymentsByItemIdAsync(
        IReadOnlyList<Guid> decisionIds,
        CancellationToken cancellationToken)
    {
        if (decisionIds.Count == 0)
            return new Dictionary<Guid, PaymentExecution>();

        return await db.PaymentExecutions
            .Where(p => decisionIds.Contains(p.CommitteeDecisionId))
            .ToDictionaryAsync(p => p.AssistanceItemId, cancellationToken);
    }

    public CommitteeDecisionDto MapDecisionForDashboard(CommitteeDecision decision, AuthorizationContext auth) =>
        MapDecision(decision, auth);

    private CommitteeDecisionDto MapDecision(
        CommitteeDecision d,
        AuthorizationContext auth,
        IReadOnlyDictionary<Guid, PaymentExecution>? paymentsByItem = null)
    {
        var payments = paymentsByItem?.Values.ToList();
        return new CommitteeDecisionDto
        {
            Id = d.Id,
            DecisionCode = d.DecisionCode,
            FamilyId = d.FamilyId,
            FamilyCode = d.Family?.FamilyCode ?? string.Empty,
            FamilyLastName = d.Family?.FamilyLastName ?? string.Empty,
            MeetingDate = d.MeetingDate,
            Summary = d.Summary,
            Status = WorkflowHelpers.ComputeDerivedDecisionStatus(d),
            CreatedByUserId = d.CreatedByUserId,
            CreatedByUserName = d.CreatedByUser?.FullName ?? string.Empty,
            TotalAmount = d.TotalAmount,
            RejectionReason = d.RejectionReason,
            SuspendReason = d.SuspendReason,
            ReturnReason = d.ReturnReason,
            CancelReason = d.CancelReason,
            ApprovalNotes = d.ApprovalNotes,
            SubmittedAt = d.SubmittedAt,
            ApprovedAt = d.ApprovedAt,
            RejectedAt = d.RejectedAt,
            SuspendedAt = d.SuspendedAt,
            ResumedAt = d.ResumedAt,
            CancelledAt = d.CancelledAt,
            WorkflowPhase = WorkflowHelpers.ComputeWorkflowPhase(d, payments),
            IsOwnedByCurrentUser = WorkflowHelpers.IsDecisionOwnedByUser(d, auth.UserId),
            AvailableActions = WorkflowHelpers.AvailableDecisionActions(d, auth),
            Version = d.Version,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            Items = d.Items.OrderBy(i => i.LineNumber).Select(i => MapItem(i, d, auth, paymentsByItem)).ToList()
        };
    }

    private static AssistanceItemDto MapItem(
        AssistanceItem i,
        CommitteeDecision parent,
        AuthorizationContext auth,
        IReadOnlyDictionary<Guid, PaymentExecution>? paymentsByItem = null)
    {
        PaymentItemSummaryDto? summary = null;
        if (paymentsByItem is not null && paymentsByItem.TryGetValue(i.Id, out var payment))
        {
            summary = new PaymentItemSummaryDto
            {
                PaymentId = payment.Id,
                Status = payment.Status,
                ReturnReason = payment.ReturnReason,
                ExecutedAt = payment.ExecutedAt,
                ProofUploadedAt = payment.ProofUploadedAt,
                PaidAt = payment.PaidAt,
                ProofFileName = payment.ProofFileName
            };
        }

        return new AssistanceItemDto
        {
            Id = i.Id,
            LineNumber = i.LineNumber,
            AssistanceTypeId = i.AssistanceTypeId,
            AssistanceTypeName = i.AssistanceType?.Name ?? string.Empty,
            Description = i.Description,
            Amount = i.Amount,
            OriginalApprovedAmount = i.OriginalApprovedAmount,
            PreviousPaymentAmount = i.PreviousPaymentAmount,
            AmountAdjustmentReason = i.AmountAdjustmentReason,
            AmountAdjustmentExplanation = i.AmountAdjustmentExplanation,
            PaymentTarget = i.PaymentTarget,
            PaymentMethod = i.PaymentMethod,
            SupplierId = i.SupplierId,
            SupplierName = i.Supplier?.Name,
            PayeeName = i.PayeeName,
            TransferBankNumber = i.TransferBankNumber,
            TransferBranchNumber = i.TransferBranchNumber,
            TransferAccountNumber = i.TransferAccountNumber,
            VoucherType = i.VoucherType,
            IsUrgent = i.IsUrgent,
            Status = i.Status,
            ExecutionStatus = i.ExecutionStatus,
            ApprovedAt = i.ApprovedAt,
            AvailableActions = WorkflowHelpers.AvailableAssistanceItemActions(i, parent, auth),
            PaymentSummary = summary,
            Version = i.Version,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt
        };
    }
}
