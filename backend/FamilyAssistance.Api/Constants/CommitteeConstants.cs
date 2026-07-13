namespace FamilyAssistance.Api.Constants;

public static class CommitteeDecisionStatuses
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string ReturnedForRevision = "returned_for_revision";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Suspended = "suspended";
    public const string Cancelled = "cancelled";
    public const string PartiallyPaid = "partially_paid";
    public const string FullyPaid = "fully_paid";

    public static readonly HashSet<string> EditableHeader =
    [
        Draft, ReturnedForRevision
    ];

    public static readonly HashSet<string> EditableItems =
    [
        Draft, ReturnedForRevision
    ];
}

public static class PaymentTargets
{
    public const string Family = "family";
    public const string Supplier = "supplier";
    public const string Other = "other";

    public static readonly HashSet<string> All = [Family, Supplier, Other];
}

public static class PaymentMethods
{
    public const string BankTransfer = "bank_transfer";
    public const string Check = "check";
    public const string Vouchers = "vouchers";

    public static readonly HashSet<string> All = [BankTransfer, Check, Vouchers];
}

public static class AssistanceItemStatuses
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Returned = "returned";
    public const string Approved = "approved";
    public const string Suspended = "suspended";
    public const string Rejected = "rejected";
    public const string WaitingForReference = "waiting_for_reference";
    public const string Paid = "paid";
    public const string Completed = "completed";

    public static readonly HashSet<string> PostSubmitListable =
    [
        Submitted, Returned, Approved, Suspended, Rejected, WaitingForReference, Paid, Completed
    ];
}

public static class PaymentExecutionStatuses
{
    public const string AwaitingPayment = "awaiting_payment";
    public const string Executing = "executing";
    public const string ProofUploaded = "proof_uploaded";
    public const string Paid = "paid";
    public const string ReturnedToCoordinator = "returned_to_coordinator";
    public const string OnHold = "on_hold";
    public const string WaitingForReference = "waiting_for_reference";

    public static readonly HashSet<string> ActiveQueue =
    [
        AwaitingPayment, Executing, ProofUploaded, WaitingForReference
    ];
}

public static class WorkflowPhases
{
    public const string Intake = "intake";
    public const string PendingManager = "pending_manager";
    public const string OnHold = "on_hold";
    public const string PendingFinance = "pending_finance";
    public const string Completed = "completed";
    public const string Closed = "closed";
}

/// <summary>Phase 16 export batch statuses.</summary>
public static class ExportBatchStatuses
{
    public const string Open = "open";
    public const string PartiallyCancelled = "partially_cancelled";
    public const string Cancelled = "cancelled";

    public static readonly HashSet<string> All = [Open, PartiallyCancelled, Cancelled];
}

/// <summary>Phase 16 export batch item statuses.</summary>
public static class ExportBatchItemStatuses
{
    public const string Active = "active";
    public const string Cancelled = "cancelled";

    public static readonly HashSet<string> All = [Active, Cancelled];
}

/// <summary>
/// Closed amount-adjustment reason list (Phase 16).
/// Hebrew: typing_error=טעות הקלדה, quote_update=עידכון הצעת מחיר,
/// quantity_change=שינוי כמות, other=אחר (free-text required).
/// </summary>
public static class AmountAdjustmentReasons
{
    public const string TypingError = "typing_error";
    public const string QuoteUpdate = "quote_update";
    public const string QuantityChange = "quantity_change";
    public const string Other = "other";

    public static readonly HashSet<string> All =
    [
        TypingError, QuoteUpdate, QuantityChange, Other
    ];

    public static bool RequiresExplanation(string? reason) =>
        string.Equals(reason, Other, StringComparison.Ordinal);
}
