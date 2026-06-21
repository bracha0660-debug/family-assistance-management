namespace FamilyAssistance.Api.Entities;

public class PaymentExecution
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CommitteeDecisionId { get; set; }
    public Guid AssistanceItemId { get; set; }
    public string Status { get; set; } = "awaiting_payment";
    public string? ExecutionReference { get; set; }
    public string? ProofFileName { get; set; }
    public string? ProofStoredFileName { get; set; }
    public string? ReturnReason { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? ProofUploadedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public CommitteeDecision? CommitteeDecision { get; set; }
    public AssistanceItem? AssistanceItem { get; set; }
}
