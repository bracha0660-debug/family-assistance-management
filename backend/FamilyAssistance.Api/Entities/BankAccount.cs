namespace FamilyAssistance.Api.Entities;

public class BankAccount
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string OwnerEntityType { get; set; } = string.Empty;
    public Guid OwnerEntityId { get; set; }
    public string BankNumber { get; set; } = string.Empty;
    public string BranchNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<BankAccountHistory> History { get; set; } = [];
}
