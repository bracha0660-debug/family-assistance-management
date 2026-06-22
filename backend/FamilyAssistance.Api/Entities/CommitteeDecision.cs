namespace FamilyAssistance.Api.Entities;

public class CommitteeDecision
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public DateOnly MeetingDate { get; set; }
    public string? Summary { get; set; }
    public string Status { get; set; } = "draft";
    public Guid CreatedByUserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? RejectionReason { get; set; }
    public string? SuspendReason { get; set; }
    public string? ReturnReason { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public Family? Family { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<AssistanceItem> Items { get; set; } = [];
}
