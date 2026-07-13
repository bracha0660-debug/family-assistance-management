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
    /// <summary>Account-holder for other/bank-transfer rows; export falls back to PayeeName when null.</summary>
    public string? AccountHolderName { get; set; }
    public ICollection<AssistanceItemHistoryEvent> HistoryEvents { get; set; } = [];
    public string? VoucherType { get; set; }
    public bool IsUrgent { get; set; }
    public string Status { get; set; } = "draft";
    public string ExecutionStatus { get; set; } = "awaiting_payment";
    public string? ExecutionReference { get; set; }
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Immutable snapshot of <see cref="Amount"/> at manager approve.
    /// Never overwrite after set. <see cref="Amount"/> remains the current payment/export amount.
    /// </summary>
    public decimal? OriginalApprovedAmount { get; set; }

    /// <summary>Previous current payment/export amount when an adjustment is applied.</summary>
    public decimal? PreviousPaymentAmount { get; set; }

    /// <summary>typing_error | quote_update | quantity_change | other</summary>
    public string? AmountAdjustmentReason { get; set; }

    /// <summary>Required only when <see cref="AmountAdjustmentReason"/> is other.</summary>
    public string? AmountAdjustmentExplanation { get; set; }

    public Guid? AmountAdjustedByUserId { get; set; }
    public DateTime? AmountAdjustedAt { get; set; }

    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public CommitteeDecision? CommitteeDecision { get; set; }
    public AssistanceType? AssistanceType { get; set; }
    public Supplier? Supplier { get; set; }
    public AssistanceItemDocument? Document { get; set; }
    public PaymentExecution? PaymentExecution { get; set; }
    public User? AmountAdjustedByUser { get; set; }
    public ICollection<ExportBatchItem> ExportBatchItems { get; set; } = [];
}
