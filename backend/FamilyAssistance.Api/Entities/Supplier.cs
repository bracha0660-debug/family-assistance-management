namespace FamilyAssistance.Api.Entities;

public class Supplier
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? Phone { get; set; }
    public string? AccountingCode { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? BankNumber { get; set; }
    public string? BranchNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public string Status { get; set; } = "active";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
}
