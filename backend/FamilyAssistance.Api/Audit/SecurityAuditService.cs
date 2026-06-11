using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Audit;

public class SecurityAuditService(AppDbContext db) : ISecurityAuditService
{
    public async Task LogAsync(SecurityAuditEntry entry, CancellationToken cancellationToken = default)
    {
        var log = new SecurityAuditLog
        {
            Id = Guid.NewGuid(),
            EventCode = entry.EventCode,
            EventType = entry.EventType,
            UsernameAttempted = entry.UsernameAttempted,
            UserId = entry.UserId,
            OrganizationId = entry.OrganizationId,
            SessionId = entry.SessionId,
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            CreatedAt = DateTime.UtcNow
        };

        db.SecurityAuditLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
    }
}
