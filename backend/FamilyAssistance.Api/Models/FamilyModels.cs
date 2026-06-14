namespace FamilyAssistance.Api.Models;

public sealed class CreateFamilyRequest
{
    public string HeadOfHouseholdName { get; set; } = string.Empty;
    public string? HeadIdNumber { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int? HouseholdSize { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateFamilyRequest
{
    public string? HeadOfHouseholdName { get; set; }
    public string? HeadIdNumber { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int? HouseholdSize { get; set; }
    public string? Notes { get; set; }
}

public sealed class DeactivateFamilyRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class FamilyDto
{
    public Guid Id { get; init; }
    public string FamilyCode { get; init; } = string.Empty;
    public string HeadOfHouseholdName { get; init; } = string.Empty;
    public string? HeadIdNumber { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public int HouseholdSize { get; init; }
    public Guid AssignedCoordinatorId { get; init; }
    public string AssignedCoordinatorName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class FamilySummaryDto
{
    public int Total { get; init; }
    public int Active { get; init; }
    public int Inactive { get; init; }
}

public sealed class FamilyListResponse
{
    public required FamilySummaryDto Summary { get; init; }
    public required IReadOnlyList<FamilyDto> Families { get; init; }
}

public sealed class FamilyResponse
{
    public required FamilyDto Family { get; init; }
}
