namespace FamilyAssistance.Api.Models;

public sealed class CreateSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string BankNumber { get; set; } = string.Empty;
    public string BranchNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public bool BankVerifiedExternally { get; set; }
}

public sealed class UpdateSupplierRequest
{
    public string? Name { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BankNumber { get; set; }
    public string? BranchNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public bool? BankVerifiedExternally { get; set; }
    public string? Reason { get; set; }
}

public sealed class DeactivateSupplierRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class RestoreSupplierRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class SupplierDto
{
    public Guid Id { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? RegistrationNumber { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string BankNumber { get; init; } = string.Empty;
    public string BranchNumber { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string AccountHolderName { get; init; } = string.Empty;
    public bool BankVerifiedExternally { get; init; }
    public string Status { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class SupplierSummaryDto
{
    public int Total { get; init; }
    public int Active { get; init; }
    public int Inactive { get; init; }
}

public sealed class SupplierListResponse
{
    public required SupplierSummaryDto Summary { get; init; }
    public required IReadOnlyList<SupplierDto> Suppliers { get; init; }
}

public sealed class SupplierResponse
{
    public required SupplierDto Supplier { get; init; }
}
