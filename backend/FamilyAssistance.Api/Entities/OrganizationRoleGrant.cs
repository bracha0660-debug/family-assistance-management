namespace FamilyAssistance.Api.Entities;

public class OrganizationRoleGrant
{
    public Guid Id { get; set; }
    public Guid OrganizationRoleId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public string Scope { get; set; } = "organization";
    public DateTime GrantedAt { get; set; }
    public Guid? GrantedByUserId { get; set; }

    public OrganizationRole OrganizationRole { get; set; } = null!;
    public PermissionCatalog Permission { get; set; } = null!;
    public User? GrantedByUser { get; set; }
}
