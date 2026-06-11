namespace FamilyAssistance.Api.Audit;

public interface ISecurityAuditService
{
    Task LogAsync(SecurityAuditEntry entry, CancellationToken cancellationToken = default);
}
