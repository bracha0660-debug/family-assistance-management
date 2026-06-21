namespace FamilyAssistance.Api.Entities;

public class Family
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string FamilyCode { get; set; } = string.Empty;
    public long AccountingCode { get; set; }
    public Guid AccountingCoordinatorId { get; set; }
    public string FamilyLastName { get; set; } = string.Empty;
    public string? FatherName { get; set; }
    public string? FatherIsraeliId { get; set; }
    public string? MotherName { get; set; }
    public string? MotherIsraeliId { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public Guid AssignedCoordinatorId { get; set; }
    public string BankNumber { get; set; } = string.Empty;
    public string BranchNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public bool BankVerifiedExternally { get; set; }
    public string Status { get; set; } = "active";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public User? AssignedCoordinator { get; set; }
    public User? AccountingCoordinator { get; set; }
}
