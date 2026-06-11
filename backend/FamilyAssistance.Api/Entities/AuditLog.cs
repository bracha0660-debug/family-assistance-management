namespace FamilyAssistance.Api.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public Guid? OrganizationId { get; set; }
    public Guid ActorUserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }

    public Organization? Organization { get; set; }
    public User ActorUser { get; set; } = null!;
}
