namespace FamilyAssistance.Api.Models;

public sealed class ActivityLogEntryDto
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string EventCode { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorUsername { get; init; } = string.Empty;
    public string ActorFullName { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Reason { get; init; }
}

public sealed class ActivityLogListResponse
{
    public required IReadOnlyList<ActivityLogEntryDto> Entries { get; init; }
    public required int ReturnedCount { get; init; }
}
