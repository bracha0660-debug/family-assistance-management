namespace FamilyAssistance.Api.Entities;

public class OrganizationRole
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? FactoryPresetKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<OrganizationRoleGrant> Grants { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}
