using System.Globalization;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

/// <summary>Phase B — append-only AssistanceItem history + closed allow-list payment edit.</summary>
public sealed class AssistanceItemHistoryService(AppDbContext db)
{
    public const string SystemActorDisplayName = "מערכת";
    public const string VersionConflictMessage = "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.";

    public async Task AppendEventAsync(
        Guid organizationId,
        Guid assistanceItemId,
        string eventType,
        Guid? actorUserId,
        string? actorDisplayName,
        string? relatedEntityType,
        Guid? relatedEntityId,
        string? reason,
        IReadOnlyList<AssistanceItemHistoryFieldChange>? fieldChanges,
        DateTime? occurredAt = null,
        CancellationToken cancellationToken = default)
    {
        var now = occurredAt ?? DateTime.UtcNow;
        var display = string.IsNullOrWhiteSpace(actorDisplayName)
            ? (actorUserId is null ? SystemActorDisplayName : actorDisplayName ?? SystemActorDisplayName)
            : actorDisplayName.Trim();

        if (actorUserId is not null && string.IsNullOrWhiteSpace(actorDisplayName))
        {
            var name = await db.Users.AsNoTracking()
                .Where(u => u.Id == actorUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            display = string.IsNullOrWhiteSpace(name) ? SystemActorDisplayName : name!;
        }

        var parent = new AssistanceItemHistoryEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AssistanceItemId = assistanceItemId,
            EventType = eventType,
            EventDescriptionHe = AssistanceItemHistoryEventTypes.DescriptionHe(eventType),
            ActorUserId = actorUserId,
            ActorDisplayName = display,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            Reason = reason,
            OccurredAt = now,
            CreatedAt = now
        };

        if (fieldChanges is { Count: > 0 })
        {
            foreach (var change in fieldChanges)
            {
                change.Id = change.Id == Guid.Empty ? Guid.NewGuid() : change.Id;
                change.HistoryEventId = parent.Id;
                parent.FieldChanges.Add(change);
            }
        }

        db.AssistanceItemHistoryEvents.Add(parent);
    }

    public async Task<ServiceResult<AssistanceItemHistoryListResponse>> ListAsync(
        Guid organizationId,
        Guid assistanceItemId,
        AssistanceItemHistoryListQuery query,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.AssistanceItemsViewHistory)
            && !auth.FullOrgAccess)
        {
            return ServiceResult<AssistanceItemHistoryListResponse>.Fail(403, "FORBIDDEN", "אין הרשאה לצפייה בהיסטוריה");
        }

        var item = await db.AssistanceItems
            .AsNoTracking()
            .Include(i => i.CommitteeDecision)
            .FirstOrDefaultAsync(i => i.Id == assistanceItemId && i.OrganizationId == organizationId, cancellationToken);
        if (item is null)
            return ServiceResult<AssistanceItemHistoryListResponse>.Fail(404, "NOT_FOUND", "פריט הסיוע לא נמצא");

        if (item.CommitteeDecision is not null
            && !ScopeEvaluator.CanAccessCommitteeDecision(auth, item.CommitteeDecision, PermissionKeys.AssistanceItemsViewHistory)
            && !auth.FullOrgAccess)
        {
            return ServiceResult<AssistanceItemHistoryListResponse>.Fail(403, "FORBIDDEN", "אין הרשאה לצפייה בהיסטוריה");
        }

        var limit = Math.Clamp(query.Limit <= 0 ? 25 : query.Limit, 1, 100);
        var offset = Math.Max(0, query.Offset);

        var baseQuery = db.AssistanceItemHistoryEvents
            .AsNoTracking()
            .Include(e => e.FieldChanges)
            .Where(e => e.OrganizationId == organizationId && e.AssistanceItemId == assistanceItemId);

        var total = await baseQuery.CountAsync(cancellationToken);
        var events = await baseQuery
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return ServiceResult<AssistanceItemHistoryListResponse>.Ok(new AssistanceItemHistoryListResponse
        {
            Events = events.Select(MapEventMasked).ToList(),
            Total = total,
            Limit = limit,
            Offset = offset
        });
    }

    public static AssistanceItemHistoryFieldChange CreateFieldChange(
        string fieldKey,
        string? previousValue,
        string? newValue,
        string valueType = "string") =>
        new()
        {
            Id = Guid.NewGuid(),
            FieldKey = fieldKey,
            FieldLabelHe = AssistanceItemEditableFields.LabelHe(fieldKey),
            PreviousValue = previousValue,
            NewValue = newValue,
            ValueType = valueType,
            IsSensitive = AssistanceItemEditableFields.Sensitive.Contains(fieldKey)
        };

    public static string MaskSensitive(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;
        if (value.Length <= 4)
            return new string('*', value.Length);
        return new string('*', Math.Max(6, value.Length - 4)) + value[^4..];
    }

    private static AssistanceItemHistoryEventDto MapEventMasked(AssistanceItemHistoryEvent e) =>
        new()
        {
            Id = e.Id,
            AssistanceItemId = e.AssistanceItemId,
            EventType = e.EventType,
            EventDescriptionHe = e.EventDescriptionHe,
            ActorUserId = e.ActorUserId,
            ActorDisplayName = string.IsNullOrWhiteSpace(e.ActorDisplayName)
                ? SystemActorDisplayName
                : e.ActorDisplayName,
            RelatedEntityType = e.RelatedEntityType,
            RelatedEntityId = e.RelatedEntityId,
            Reason = e.Reason,
            OccurredAt = e.OccurredAt,
            FieldChanges = e.FieldChanges
                .OrderBy(c => c.FieldKey)
                .Select(c => new AssistanceItemHistoryFieldChangeDto
                {
                    Id = c.Id,
                    FieldKey = c.FieldKey,
                    FieldLabelHe = c.FieldLabelHe,
                    PreviousValue = c.IsSensitive ? MaskSensitive(c.PreviousValue) : c.PreviousValue,
                    NewValue = c.IsSensitive ? MaskSensitive(c.NewValue) : c.NewValue,
                    ValueType = c.ValueType,
                    IsSensitive = c.IsSensitive
                })
                .ToList()
        };

    public static string FormatDecimal(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
