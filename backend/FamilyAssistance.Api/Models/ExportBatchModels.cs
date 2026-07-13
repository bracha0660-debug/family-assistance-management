namespace FamilyAssistance.Api.Models;

public sealed class PaymentRowListQuery
{
    public string? Status { get; set; }
    public string? Section { get; set; }
    public int? MinAgeDays { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; }
}

/// <summary>Phase 16 operational payment row (AssistanceItem-based, may lack PE until export).</summary>
public sealed class PaymentRowDto
{
    public Guid AssistanceItemId { get; init; }
    public Guid? PaymentExecutionId { get; init; }
    public Guid CommitteeDecisionId { get; init; }
    public string DecisionCode { get; init; } = string.Empty;
    public Guid FamilyId { get; init; }
    public string FamilyCode { get; init; } = string.Empty;
    public long FamilyAccountingCode { get; init; }
    public string FamilyLastName { get; init; } = string.Empty;
    public Guid AssistanceTypeId { get; init; }
    public string AssistanceTypeName { get; init; } = string.Empty;
    public string AssistanceTypeCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal? OriginalApprovedAmount { get; init; }
    public decimal? PreviousPaymentAmount { get; init; }
    public string? AmountAdjustmentReason { get; init; }
    public string? AmountAdjustmentExplanation { get; init; }
    public bool HasAmountAdjustment =>
        OriginalApprovedAmount is not null && Amount != OriginalApprovedAmount.Value;
    public string PaymentTarget { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public string? SupplierAccountingCode { get; init; }
    public string? PayeeName { get; init; }
    public string? TransferBankNumber { get; init; }
    public string? TransferBranchNumber { get; init; }
    public string? TransferAccountNumber { get; init; }
    public string? AccountHolderName { get; init; }
    public string? VoucherType { get; init; }
    public bool IsUrgent { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ExecutionReference { get; init; }
    public Guid? ActiveExportBatchId { get; init; }
    public string? ActiveExportBatchNumber { get; init; }
    public Guid? ActiveExportBatchItemId { get; init; }
    public bool EligibleForExport { get; init; }
    public IReadOnlyList<string> AvailableActions { get; init; } = [];
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class PaymentRowListResponse
{
    public required IReadOnlyList<PaymentRowDto> Items { get; init; }
    public required PaymentRowSummaryDto Summary { get; init; }
}

public sealed class PaymentRowSummaryDto
{
    public int Total { get; init; }
    public int Approved { get; init; }
    public int WaitingForReference { get; init; }
    public int Paid { get; init; }
    public int Completed { get; init; }
}

public sealed class PaymentRowResponse
{
    public required PaymentRowDto Item { get; init; }
}

public sealed class CreateExportBatchRequest
{
    public List<ExportBatchSelection> Items { get; set; } = [];
}

public sealed class ExportBatchSelection
{
    public Guid AssistanceItemId { get; set; }
    public int Version { get; set; }
}

public sealed class CancelExportBatchRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class CancelExportBatchItemRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class AdjustPaymentAmountRequest
{
    public decimal NewAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Explanation { get; set; }
}

public sealed class ExportBatchDto
{
    public Guid Id { get; init; }
    public string BatchNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public long? FileSizeBytes { get; init; }
    public DateTime? GeneratedAt { get; init; }
    public int TotalItemCount { get; init; }
    public int ActiveItemCount { get; init; }
    public int CancelledItemCount { get; init; }
    public IReadOnlyList<string> AvailableActions { get; init; } = [];
    public IReadOnlyList<ExportBatchItemDto>? Items { get; init; }
}

public sealed class ExportBatchItemDto
{
    public Guid Id { get; init; }
    public Guid AssistanceItemId { get; init; }
    public Guid PaymentExecutionId { get; init; }
    public decimal ExportedAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? CancelReason { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string DecisionCode { get; init; } = string.Empty;
    public string FamilyCode { get; init; } = string.Empty;
    public long? FamilyAccountingCode { get; init; }
    public string FamilyName { get; init; } = string.Empty;
    public string AssistanceTypeName { get; init; } = string.Empty;
    public string AssistanceTypeCode { get; init; } = string.Empty;
    public decimal OriginalApprovedAmount { get; init; }
    public string? AmountAdjustmentReason { get; init; }
    public string? AmountAdjustmentExplanation { get; init; }
    public string? SupplierName { get; init; }
    public string? SupplierAccountingCode { get; init; }
    public string PaymentTarget { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string? PayeeName { get; init; }
    public string? ExecutionReference { get; init; }
}

public sealed class ExportBatchListResponse
{
    public required IReadOnlyList<ExportBatchDto> Batches { get; init; }
}

public sealed class ExportBatchResponse
{
    public required ExportBatchDto Batch { get; init; }
}

public sealed class ExportBatchRowValidationError
{
    public Guid AssistanceItemId { get; init; }
    public string DecisionCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
