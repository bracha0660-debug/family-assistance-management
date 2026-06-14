namespace FamilyAssistance.Api.Entities;

public class Family
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string FamilyCode { get; set; } = string.Empty;
    public string HeadOfHouseholdName { get; set; } = string.Empty;
    public string? HeadIdNumber { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int HouseholdSize { get; set; }
    public Guid AssignedCoordinatorId { get; set; }
    public string Status { get; set; } = "active";
    public string? Notes { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public User? AssignedCoordinator { get; set; }
}
