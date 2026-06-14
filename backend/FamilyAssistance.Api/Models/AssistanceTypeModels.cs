namespace FamilyAssistance.Api.Models;

public sealed class CreateAssistanceTypeRequest
{
    public string TypeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? DefaultAmount { get; set; }
    public string Frequency { get; set; } = string.Empty;
}

public sealed class UpdateAssistanceTypeRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? DefaultAmount { get; set; }
    public bool ClearDefaultAmount { get; set; }
    public string? Frequency { get; set; }
}

public sealed class DeactivateAssistanceTypeRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class AssistanceTypeDto
{
    public Guid Id { get; init; }
    public string TypeCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal? DefaultAmount { get; init; }
    public string Currency { get; init; } = "ILS";
    public string Frequency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class AssistanceTypeSummaryDto
{
    public int Total { get; init; }
    public int Active { get; init; }
    public int Inactive { get; init; }
}

public sealed class AssistanceTypeListResponse
{
    public required AssistanceTypeSummaryDto Summary { get; init; }
    public required IReadOnlyList<AssistanceTypeDto> AssistanceTypes { get; init; }
}

public sealed class AssistanceTypeResponse
{
    public required AssistanceTypeDto AssistanceType { get; init; }
}
