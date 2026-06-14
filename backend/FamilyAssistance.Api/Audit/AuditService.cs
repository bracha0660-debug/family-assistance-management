using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Audit;

public class AuditService(AppDbContext db) : IAuditService
{
    private static readonly HashSet<string> MaterialActions =
    [
        "status_change",
        "bank_account_change",
        "amount_change",
        "supplier_change",
        "user_disable",
        "organization_suspend",
        "family_deactivate",
        "assistance_type_deactivate"
    ];

    public void Stage(AuditEntry entry)
    {
        db.AuditLogs.Add(CreateLog(entry));
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Stage(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AuditLog CreateLog(AuditEntry entry)
    {
        if (MaterialActions.Contains(entry.Action))
        {
            var reason = entry.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < 3)
                throw new ArgumentException("יש לציין סיבה לשינוי מהותי");
        }

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            EventCode = entry.EventCode,
            OrganizationId = entry.OrganizationId,
            ActorUserId = entry.ActorUserId,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Action = entry.Action,
            FieldName = entry.FieldName,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            Reason = entry.Reason,
            CreatedAt = DateTime.UtcNow
        };
    }
}
