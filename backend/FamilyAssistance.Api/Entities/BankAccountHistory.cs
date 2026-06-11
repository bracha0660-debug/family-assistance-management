namespace FamilyAssistance.Api.Entities;

public class BankAccountHistory
{
    public Guid Id { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid OrganizationId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string BankNumber { get; set; } = string.Empty;
    public string BranchNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime CreatedAt { get; set; }

    public BankAccount BankAccount { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
