namespace FamilyAssistance.Api.Models;

public sealed class CreateFamilyRequest
{
    public string FamilyLastName { get; set; } = string.Empty;
    public long? AccountingCode { get; set; }
    public Guid? AssignedCoordinatorId { get; set; }
    public string? FatherName { get; set; }
    public string? FatherIsraeliId { get; set; }
    public string? MotherName { get; set; }
    public string? MotherIsraeliId { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string BankNumber { get; set; } = string.Empty;
    public string BranchNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public bool BankVerifiedExternally { get; set; }
}

public sealed class UpdateFamilyRequest
{
    public string? FamilyLastName { get; set; }
    public long? AccountingCode { get; set; }
    public string? FatherName { get; set; }
    public string? FatherIsraeliId { get; set; }
    public string? MotherName { get; set; }
    public string? MotherIsraeliId { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BankNumber { get; set; }
    public string? BranchNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public bool? BankVerifiedExternally { get; set; }
    public Guid? AssignedCoordinatorId { get; set; }
    public string? Reason { get; set; }
}

public sealed class DeactivateFamilyRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class RestoreFamilyRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class SuggestedAccountingCodeResponse
{
    public Guid AccountingCoordinatorId { get; init; }
    public long SuggestedAccountingCode { get; init; }
}

public sealed class FamilyDto
{
    public Guid Id { get; init; }
    public string FamilyCode { get; init; } = string.Empty;
    public long AccountingCode { get; init; }
    public Guid AccountingCoordinatorId { get; init; }
    public string FamilyLastName { get; init; } = string.Empty;
    public string? FatherName { get; init; }
    public string? FatherIsraeliId { get; init; }
    public string? MotherName { get; init; }
    public string? MotherIsraeliId { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string BankNumber { get; init; } = string.Empty;
    public string BranchNumber { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string AccountHolderName { get; init; } = string.Empty;
    public bool BankVerifiedExternally { get; init; }
    public Guid AssignedCoordinatorId { get; init; }
    public string AssignedCoordinatorName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
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
