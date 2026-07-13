namespace FamilyAssistance.Api.Models;

public sealed class EditAssistanceItemPaymentRequest
{
    public Dictionary<string, string?> Fields { get; set; } = new();
    /// <summary>Required when amount changes.</summary>
    public string? AmountAdjustmentReason { get; set; }
    public string? AmountAdjustmentExplanation { get; set; }
}

public sealed class AssistanceItemHistoryListQuery
{
    public int Limit { get; set; } = 25;
    public int Offset { get; set; }
}

public sealed class AssistanceItemHistoryEventDto
{
    public Guid Id { get; init; }
    public Guid AssistanceItemId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string EventDescriptionHe { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }
    public string ActorDisplayName { get; init; } = string.Empty;
    public string? RelatedEntityType { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public string? Reason { get; init; }
    public DateTime OccurredAt { get; init; }
    public IReadOnlyList<AssistanceItemHistoryFieldChangeDto> FieldChanges { get; init; } = [];
}

public sealed class AssistanceItemHistoryFieldChangeDto
{
    public Guid Id { get; init; }
    public string FieldKey { get; init; } = string.Empty;
    public string FieldLabelHe { get; init; } = string.Empty;
    public string? PreviousValue { get; init; }
    public string? NewValue { get; init; }
    public string ValueType { get; init; } = "string";
    public bool IsSensitive { get; init; }
}

public sealed class AssistanceItemHistoryListResponse
{
    public required IReadOnlyList<AssistanceItemHistoryEventDto> Events { get; init; }
    public required int Total { get; init; }
    public required int Limit { get; init; }
    public required int Offset { get; init; }
}
