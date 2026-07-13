namespace FamilyAssistance.Api.Entities;

public class OrganizationRolePermission
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
    public Guid? GrantedByUserId { get; set; }

    public Organization Organization { get; set; } = null!;
    public PermissionCatalog Permission { get; set; } = null!;
    public User? GrantedByUser { get; set; }
}
