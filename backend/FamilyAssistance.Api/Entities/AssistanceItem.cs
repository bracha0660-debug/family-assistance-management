namespace FamilyAssistance.Api.Entities;

public class AssistanceItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CommitteeDecisionId { get; set; }
    public int LineNumber { get; set; }
    public Guid AssistanceTypeId { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string PaymentTarget { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? PayeeName { get; set; }
    public string? TransferBankNumber { get; set; }
    public string? TransferBranchNumber { get; set; }
    public string? TransferAccountNumber { get; set; }
    public string? VoucherType { get; set; }
    public bool IsUrgent { get; set; }
    public string ExecutionStatus { get; set; } = "awaiting_payment";
    public string? ExecutionReference { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public CommitteeDecision? CommitteeDecision { get; set; }
    public AssistanceType? AssistanceType { get; set; }
    public Supplier? Supplier { get; set; }
    public AssistanceItemDocument? Document { get; set; }
    public PaymentExecution? PaymentExecution { get; set; }
}
