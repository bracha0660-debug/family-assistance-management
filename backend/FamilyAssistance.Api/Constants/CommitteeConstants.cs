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

public static class PaymentExecutionStatuses
{
    public const string AwaitingPayment = "awaiting_payment";
    public const string Executing = "executing";
    public const string ProofUploaded = "proof_uploaded";
    public const string Paid = "paid";
    public const string ReturnedToCoordinator = "returned_to_coordinator";
    public const string OnHold = "on_hold";

    public static readonly HashSet<string> ActiveQueue =
    [
        AwaitingPayment, Executing, ProofUploaded
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
