namespace FamilyAssistance.Api.Audit;

public interface IAuditService
{
    void Stage(AuditEntry entry);
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
