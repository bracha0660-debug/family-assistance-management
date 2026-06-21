namespace FamilyAssistance.Api.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? OrganizationRoleId { get; set; }
    public string Status { get; set; } = "active";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public OrganizationRole? OrganizationRole { get; set; }
    public ICollection<UserSession> Sessions { get; set; } = [];
}
