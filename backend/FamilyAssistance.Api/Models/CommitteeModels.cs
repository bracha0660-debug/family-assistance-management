namespace FamilyAssistance.Api.Models;

public sealed class CreateCommitteeDecisionRequest
{
    public Guid FamilyId { get; set; }
    public DateOnly MeetingDate { get; set; }
    public string? Summary { get; set; }
}

public sealed class UpdateCommitteeDecisionRequest
{
    public DateOnly? MeetingDate { get; set; }
    public string? Summary { get; set; }
}

public sealed class StatusTransitionRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class ApproveCommitteeDecisionRequest
{
    public string? Reason { get; set; }
}

public sealed class ResumeCommitteeDecisionRequest
{
    public string? Reason { get; set; }
}

public sealed class CommitteeDecisionListQuery
{
    public string? Status { get; set; }
    public string? WorkflowPhase { get; set; }
    public string? Ownership { get; set; }
    public string? Section { get; set; }
    public string? Q { get; set; }
    public Guid? FamilyId { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; }
}

public sealed class RejectCommitteeDecisionRequest
{
    public string Reason { get; set; } = string.Empty;
    public bool ReturnForRevision { get; set; }
}

public sealed class CreateAssistanceItemRequest
{
    public Guid AssistanceTypeId { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string PaymentTarget { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? PayeeName { get; set; }
    public string? VoucherType { get; set; }
    public bool IsUrgent { get; set; }
}

public sealed class UpdateAssistanceItemRequest
{
    public Guid? AssistanceTypeId { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public string? PaymentTarget { get; set; }
    public string? PaymentMethod { get; set; }
    public Guid? SupplierId { get; set; }
    public bool ClearSupplierId { get; set; }
    public string? PayeeName { get; set; }
    public string? VoucherType { get; set; }
    public bool? IsUrgent { get; set; }
}

public sealed class AssistanceItemDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid AssistanceTypeId { get; init; }
    public string AssistanceTypeName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public string PaymentTarget { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public string? PayeeName { get; init; }
    public string? VoucherType { get; init; }
    public bool IsUrgent { get; init; }
    public string ExecutionStatus { get; init; } = string.Empty;
    public PaymentItemSummaryDto? PaymentSummary { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class PaymentItemSummaryDto
{
    public Guid? PaymentId { get; init; }
    public string? Status { get; init; }
    public string? ReturnReason { get; init; }
    public DateTime? ExecutedAt { get; init; }
    public DateTime? ProofUploadedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public string? ProofFileName { get; init; }
}

public sealed class CommitteeDecisionDto
{
    public Guid Id { get; init; }
    public string DecisionCode { get; init; } = string.Empty;
    public Guid FamilyId { get; init; }
    public string FamilyCode { get; init; } = string.Empty;
    public string FamilyLastName { get; init; } = string.Empty;
    public DateOnly MeetingDate { get; init; }
    public string? Summary { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid CreatedByUserId { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string? RejectionReason { get; init; }
    public string? SuspendReason { get; init; }
    public string? ReturnReason { get; init; }
    public string? CancelReason { get; init; }
    public string? ApprovalNotes { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? RejectedAt { get; init; }
    public DateTime? SuspendedAt { get; init; }
    public DateTime? ResumedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string WorkflowPhase { get; init; } = string.Empty;
    public bool IsOwnedByCurrentUser { get; init; }
    public IReadOnlyList<string> AvailableActions { get; init; } = [];
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<AssistanceItemDto> Items { get; init; } = [];
}

public sealed class CommitteeDecisionSummaryDto
{
    public int Total { get; init; }
    public int Draft { get; init; }
    public int Submitted { get; init; }
    public int Approved { get; init; }
}

public sealed class CommitteeDecisionListResponse
{
    public required CommitteeDecisionSummaryDto Summary { get; init; }
    public required IReadOnlyList<CommitteeDecisionDto> Decisions { get; init; }
}

public sealed class CommitteeDecisionResponse
{
    public required CommitteeDecisionDto Decision { get; init; }
}

public sealed class AssistanceItemResponse
{
    public required AssistanceItemDto Item { get; init; }
    public int DecisionVersion { get; init; }
}
