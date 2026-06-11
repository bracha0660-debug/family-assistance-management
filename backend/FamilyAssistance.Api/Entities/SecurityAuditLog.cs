namespace FamilyAssistance.Api.Entities;

public class SecurityAuditLog
{
    public Guid Id { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string UsernameAttempted { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public Organization? Organization { get; set; }
    public UserSession? Session { get; set; }
}
