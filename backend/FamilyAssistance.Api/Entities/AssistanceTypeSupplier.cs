namespace FamilyAssistance.Api.Entities;

public class AssistanceTypeSupplier
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AssistanceTypeId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Organization? Organization { get; set; }
    public AssistanceType? AssistanceType { get; set; }
    public Supplier? Supplier { get; set; }
}
