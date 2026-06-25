namespace FamilyAssistance.Api.Entities;

public class AssistanceType
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? DefaultAmount { get; set; }
    public string Currency { get; set; } = "ILS";
    public string Frequency { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public ICollection<AssistanceTypeSupplier> RelatedSupplierLinks { get; set; } = [];
}
