namespace FamilyAssistance.Api.Entities;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string Status { get; set; } = "active";
    public int Version { get; set; } = 1;
    public int FamilyCodeCounter { get; set; }
    public int SupplierCodeCounter { get; set; }
    public int DecisionCodeCounter { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Family> Families { get; set; } = [];
    public ICollection<Supplier> Suppliers { get; set; } = [];
    public ICollection<AssistanceType> AssistanceTypes { get; set; } = [];
    public ICollection<CommitteeDecision> CommitteeDecisions { get; set; } = [];
    public ICollection<OrganizationRolePermission> RolePermissions { get; set; } = [];
    public ICollection<OrganizationRole> OrganizationRoles { get; set; } = [];
}
