namespace FamilyAssistance.Api.Entities;

/// <summary>
/// One payment row inside an export batch (Phase 16).
/// Soft-cancel only — never hard-delete historical rows.
/// Active participation uniqueness is enforced at DB level on PaymentExecutionId.
/// </summary>
public class ExportBatchItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ExportBatchId { get; set; }
    public Guid PaymentExecutionId { get; set; }
    public Guid AssistanceItemId { get; set; }

    /// <summary>Current payment/export amount frozen at export time.</summary>
    public decimal ExportedAmount { get; set; }

    /// <summary>active | cancelled</summary>
    public string Status { get; set; } = "active";

    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    // Historical snapshot (export integrity / re-download)
    public string DecisionCode { get; set; } = string.Empty;
    public string FamilyCode { get; set; } = string.Empty;
    public long? FamilyAccountingCode { get; set; }
    public string FamilyName { get; set; } = string.Empty;
    public string AssistanceTypeName { get; set; } = string.Empty;
    /// <summary>קוד סוג סיוע — snapshot of AssistanceType.TypeCode</summary>
    public string AssistanceTypeCode { get; set; } = string.Empty;
    public decimal OriginalApprovedAmount { get; set; }
    public string? AmountAdjustmentReason { get; set; }
    public string? AmountAdjustmentExplanation { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierAccountingCode { get; set; }
    public string PaymentTarget { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string? PayeeName { get; set; }
    public string? TransferBankNumber { get; set; }
    public string? TransferBranchNumber { get; set; }
    public string? TransferAccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public string? ExecutionReference { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public ExportBatch? ExportBatch { get; set; }
    public PaymentExecution? PaymentExecution { get; set; }
    public AssistanceItem? AssistanceItem { get; set; }
    public User? CancelledByUser { get; set; }
}
