namespace FamilyAssistance.Api.Audit;

public sealed class SecurityAuditEntry
{
    public required string EventCode { get; init; }
    public required string EventType { get; init; }
    public required string UsernameAttempted { get; init; }
    public Guid? UserId { get; init; }
    public Guid? OrganizationId { get; init; }
    public Guid? SessionId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
