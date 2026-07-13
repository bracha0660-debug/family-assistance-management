namespace FamilyAssistance.Api.Entities;

public class UserPermissionOverride
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public Guid? GrantedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
    public PermissionCatalog Permission { get; set; } = null!;
    public User? GrantedByUser { get; set; }
}
