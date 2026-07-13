namespace FamilyAssistance.Api.Entities;

/// <summary>Phase B — append-only parent history event for an AssistanceItem.</summary>
public class AssistanceItemHistoryEvent
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AssistanceItemId { get; set; }
    /// <summary>Stable technical event type (e.g. item_edited, approved).</summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>Hebrew user-facing event description.</summary>
    public string EventDescriptionHe { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    /// <summary>Display name snapshot; use מערכת when ActorUserId is null.</summary>
    public string ActorDisplayName { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Organization? Organization { get; set; }
    public AssistanceItem? AssistanceItem { get; set; }
    public User? ActorUser { get; set; }
    public ICollection<AssistanceItemHistoryFieldChange> FieldChanges { get; set; } = [];
}

/// <summary>Child field-change under a parent history event (one save = one parent + N children).</summary>
public class AssistanceItemHistoryFieldChange
{
    public Guid Id { get; set; }
    public Guid HistoryEventId { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string FieldLabelHe { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string ValueType { get; set; } = "string";
    public bool IsSensitive { get; set; }

    public AssistanceItemHistoryEvent? HistoryEvent { get; set; }
}
