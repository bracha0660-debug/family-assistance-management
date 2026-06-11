namespace FamilyAssistance.Api.Audit;

public sealed class AuditEntry
{
    public required string EventCode { get; init; }
    public Guid? OrganizationId { get; init; }
    public required Guid ActorUserId { get; init; }
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public required string Action { get; init; }
    public string? FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Reason { get; init; }
}
