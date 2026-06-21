namespace FamilyAssistance.Api.Models;

public sealed class ExecutePaymentRequest
{
    public string? ExecutionReference { get; set; }
}

public sealed class MarkPaidRequest
{
    public string? ExecutionReference { get; set; }
}

public sealed class ReturnPaymentRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class PaymentQueueItemDto
{
    public Guid Id { get; init; }
    public Guid CommitteeDecisionId { get; init; }
    public string DecisionCode { get; init; } = string.Empty;
    public Guid AssistanceItemId { get; init; }
    public int LineNumber { get; init; }
    public Guid FamilyId { get; init; }
    public string FamilyCode { get; init; } = string.Empty;
    public string FamilyLastName { get; init; } = string.Empty;
    public string AssistanceTypeName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string PaymentTarget { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string? SupplierName { get; init; }
    public string? PayeeName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ExecutionReference { get; init; }
    public string? ProofFileName { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class PaymentQueueSummaryDto
{
    public int Total { get; init; }
    public int AwaitingPayment { get; init; }
    public int Executing { get; init; }
    public int ProofUploaded { get; init; }
}

public sealed class PaymentQueueListResponse
{
    public required PaymentQueueSummaryDto Summary { get; init; }
    public required IReadOnlyList<PaymentQueueItemDto> Payments { get; init; }
}

public sealed class PaymentResponse
{
    public required PaymentQueueItemDto Payment { get; init; }
}

public sealed class UploadProofMetadata
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}
