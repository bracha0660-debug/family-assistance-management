using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
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
        CancellationToken cancellationToken = default)
    {
        var query = ScopeEvaluator.ApplyCommitteeListScope(
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

        var decisions = await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = decisions.Select(MapDecision).ToList();
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

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision.Family!, PermissionKeys.CommitteeDecisionsView))
            return ServiceResult<CommitteeDecisionDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision));
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
                IsUrgent = request.IsUrgent,
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
            return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision));
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

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision.Family!, PermissionKeys.CommitteeDecisionsEditDraft))
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

        if (request.IsUrgent is not null && request.IsUrgent != decision.IsUrgent)
        {
            changes.Add(("is_urgent", decision.IsUrgent.ToString(), request.IsUrgent.Value.ToString()));
            decision.IsUrgent = request.IsUrgent.Value;
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

        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision));
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
        if (entity.Items.Count == 0)
            return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", "יש להוסיף לפחות פריט סיוע אחד");

        foreach (var item in entity.Items)
        {
            var itemErrors = ValidateItemFields(item.PaymentTarget, item.PaymentMethod, item.SupplierId, item.PayeeName, item.VoucherType);
            if (itemErrors.Count > 0)
                return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", itemErrors[0], itemErrors);
        }

        return await TransitionStatusAsync(entity, CommitteeDecisionStatuses.Submitted, auth,
            d => d.SubmittedAt = DateTime.UtcNow, null, cancellationToken);
    }

    public async Task<ServiceResult<CommitteeDecisionDto>> ApproveAsync(
        Guid organizationId,
        Guid id,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var decisionResult = await LoadDecisionForTransitionAsync(organizationId, id, expectedVersion, auth,
            PermissionKeys.CommitteeDecisionsApprove,
            [CommitteeDecisionStatuses.Submitted],
            cancellationToken);
        if (!decisionResult.IsSuccess)
            return ServiceResult<CommitteeDecisionDto>.Fail(decisionResult.StatusCode, decisionResult.Code, decisionResult.Error);

        var decision = decisionResult.Value!;
        var now = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            decision.Status = CommitteeDecisionStatuses.Approved;
            decision.ApprovedAt = now;
            decision.Version++;
            decision.UpdatedAt = now;

            foreach (var item in decision.Items)
            {
                item.ExecutionStatus = PaymentExecutionStatuses.AwaitingPayment;
                item.UpdatedAt = now;

                var payment = new PaymentExecution
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    CommitteeDecisionId = decision.Id,
                    AssistanceItemId = item.Id,
                    Status = PaymentExecutionStatuses.AwaitingPayment,
                    Version = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.PaymentExecutions.Add(payment);
            }

            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.CommitteeDecisionStatusChange,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "committee_decision",
                EntityId = decision.Id,
                Action = "approve",
                FieldName = "status",
                OldValue = CommitteeDecisionStatuses.Submitted,
                NewValue = CommitteeDecisionStatuses.Approved
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

        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision));
    }

    public async Task<ServiceResult<CommitteeDecisionDto>> RejectAsync(
        Guid organizationId,
        Guid id,
        RejectCommitteeDecisionRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<CommitteeDecisionDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var newStatus = request.ReturnForRevision
            ? CommitteeDecisionStatuses.ReturnedForRevision
            : CommitteeDecisionStatuses.Rejected;

        var decisionResult = await LoadDecisionForTransitionAsync(organizationId, id, expectedVersion, auth,
            PermissionKeys.CommitteeDecisionsReject,
            [CommitteeDecisionStatuses.Submitted],
            cancellationToken);
        if (!decisionResult.IsSuccess)
            return ServiceResult<CommitteeDecisionDto>.Fail(decisionResult.StatusCode, decisionResult.Code, decisionResult.Error);

        var decision = decisionResult.Value!;
        if (request.ReturnForRevision)
        {
            decision.ReturnReason = reason;
            decision.RejectedAt = null;
            decision.RejectionReason = null;
        }
        else
        {
            decision.RejectionReason = reason;
            decision.RejectedAt = DateTime.UtcNow;
        }

        return await TransitionStatusAsync(decision, newStatus, auth, _ => { }, reason, cancellationToken);
    }

    public async Task<ServiceResult<CommitteeDecisionDto>> SuspendAsync(
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

        var decisionResult = await LoadDecisionForTransitionAsync(organizationId, id, expectedVersion, auth,
            PermissionKeys.CommitteeDecisionsApprove,
            [CommitteeDecisionStatuses.Approved, CommitteeDecisionStatuses.PartiallyPaid],
            cancellationToken);
        if (!decisionResult.IsSuccess)
            return ServiceResult<CommitteeDecisionDto>.Fail(decisionResult.StatusCode, decisionResult.Code, decisionResult.Error);

        var decision = decisionResult.Value!;
        decision.SuspendReason = reason;
        decision.SuspendedAt = DateTime.UtcNow;

        return await TransitionStatusAsync(decision, CommitteeDecisionStatuses.Suspended, auth, _ => { }, reason, cancellationToken);
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

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision.Family!, PermissionKeys.AssistanceItemsCreate))
            return ServiceResult<(AssistanceItemDto, int)>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (decision.Version != expectedVersion)
            return ServiceResult<(AssistanceItemDto, int)>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        if (!CommitteeDecisionStatuses.EditableItems.Contains(decision.Status))
            return ServiceResult<(AssistanceItemDto, int)>.Fail(409, "INVALID_STATUS", "לא ניתן לערוך פריטים במצב זה");

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

        if (request.SupplierId is not null)
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(
                s => s.Id == request.SupplierId && s.OrganizationId == organizationId && s.Status == "active",
                cancellationToken);
            if (supplier is null)
                return ServiceResult<(AssistanceItemDto, int)>.Fail(400, "VALIDATION_ERROR", "ספק לא חוקי");
        }

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
            VoucherType = NormalizeOptional(request.VoucherType),
            ExecutionStatus = PaymentExecutionStatuses.AwaitingPayment,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

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
        if (request.SupplierId is not null)
            item.Supplier = await db.Suppliers.FindAsync([request.SupplierId], cancellationToken);

        return ServiceResult<(AssistanceItemDto Item, int DecisionVersion)>.Ok((MapItem(item), decision.Version));
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

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision.Family!, PermissionKeys.AssistanceItemsEdit))
            return ServiceResult<AssistanceItemDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (!CommitteeDecisionStatuses.EditableItems.Contains(decision.Status))
            return ServiceResult<AssistanceItemDto>.Fail(409, "INVALID_STATUS", "לא ניתן לערוך פריטים במצב זה");

        var item = decision.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return ServiceResult<AssistanceItemDto>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (item.Version != expectedVersion)
            return ServiceResult<AssistanceItemDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var oldAmount = item.Amount;
        ApplyItemUpdates(request, item, out var errors);
        if (errors.Count > 0)
            return ServiceResult<AssistanceItemDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var validationErrors = ValidateItemFields(
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

        if (request.SupplierId is not null || request.ClearSupplierId)
        {
            if (request.ClearSupplierId)
                item.Supplier = null;
            else if (request.SupplierId is not null)
            {
                var supplier = await db.Suppliers.FirstOrDefaultAsync(
                    s => s.Id == request.SupplierId && s.OrganizationId == organizationId && s.Status == "active",
                    cancellationToken);
                if (supplier is null)
                    return ServiceResult<AssistanceItemDto>.Fail(400, "VALIDATION_ERROR", "ספק לא חוקי");
                item.Supplier = supplier;
            }
        }

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

        return ServiceResult<AssistanceItemDto>.Ok(MapItem(item));
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

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision.Family!, PermissionKeys.AssistanceItemsRemoveDraft))
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

        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision));
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

        if (!ScopeEvaluator.CanAccessCommitteeDecision(auth, decision.Family!, permissionKey))
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

        return ServiceResult<CommitteeDecisionDto>.Ok(MapDecision(decision));
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

        errors.AddRange(ValidateItemFields(
            request.PaymentTarget?.Trim() ?? string.Empty,
            request.PaymentMethod?.Trim() ?? string.Empty,
            request.SupplierId,
            request.PayeeName,
            request.VoucherType));
        return errors;
    }

    private static List<string> ValidateItemFields(
        string paymentTarget,
        string paymentMethod,
        Guid? supplierId,
        string? payeeName,
        string? voucherType)
    {
        var errors = new List<string>();

        if (!PaymentTargets.All.Contains(paymentTarget))
            errors.Add("יעד תשלום לא חוקי");
        if (!PaymentMethods.All.Contains(paymentMethod))
            errors.Add("אמצעי תשלום לא חוקי");

        if (paymentTarget == PaymentTargets.Supplier && supplierId is null)
            errors.Add("יש לבחור ספק");
        if (paymentTarget == PaymentTargets.Other && string.IsNullOrWhiteSpace(payeeName))
            errors.Add("יש לציין שם מוטב");
        if (paymentMethod == PaymentMethods.Vouchers && string.IsNullOrWhiteSpace(voucherType))
            errors.Add("יש לציין סוג שובר");

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
        if (request.VoucherType is not null)
            item.VoucherType = NormalizeOptional(request.VoucherType);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static CommitteeDecisionDto MapDecision(CommitteeDecision d) => new()
    {
        Id = d.Id,
        DecisionCode = d.DecisionCode,
        FamilyId = d.FamilyId,
        FamilyCode = d.Family?.FamilyCode ?? string.Empty,
        FamilyLastName = d.Family?.FamilyLastName ?? string.Empty,
        MeetingDate = d.MeetingDate,
        IsUrgent = d.IsUrgent,
        Summary = d.Summary,
        Status = d.Status,
        CreatedByUserId = d.CreatedByUserId,
        CreatedByUserName = d.CreatedByUser?.FullName ?? string.Empty,
        TotalAmount = d.TotalAmount,
        RejectionReason = d.RejectionReason,
        SuspendReason = d.SuspendReason,
        ReturnReason = d.ReturnReason,
        CancelReason = d.CancelReason,
        SubmittedAt = d.SubmittedAt,
        ApprovedAt = d.ApprovedAt,
        RejectedAt = d.RejectedAt,
        SuspendedAt = d.SuspendedAt,
        CancelledAt = d.CancelledAt,
        Version = d.Version,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        Items = d.Items.OrderBy(i => i.LineNumber).Select(MapItem).ToList()
    };

    private static AssistanceItemDto MapItem(AssistanceItem i) => new()
    {
        Id = i.Id,
        LineNumber = i.LineNumber,
        AssistanceTypeId = i.AssistanceTypeId,
        AssistanceTypeName = i.AssistanceType?.Name ?? string.Empty,
        Description = i.Description,
        Amount = i.Amount,
        PaymentTarget = i.PaymentTarget,
        PaymentMethod = i.PaymentMethod,
        SupplierId = i.SupplierId,
        SupplierName = i.Supplier?.Name,
        PayeeName = i.PayeeName,
        VoucherType = i.VoucherType,
        ExecutionStatus = i.ExecutionStatus,
        Version = i.Version,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };
}
