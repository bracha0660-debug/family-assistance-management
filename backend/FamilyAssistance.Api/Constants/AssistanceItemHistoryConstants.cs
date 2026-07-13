namespace FamilyAssistance.Api.Constants;

/// <summary>Phase B — stable history event types + closed editable allow-list.</summary>
public static class AssistanceItemHistoryEventTypes
{
    public const string ItemCreated = "item_created";
    public const string Submitted = "submitted";
    public const string Resubmitted = "resubmitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Returned = "returned";
    public const string Suspended = "suspended";
    public const string ItemEdited = "item_edited";
    public const string ExportBatchCreated = "export_batch_created";
    public const string ExportItemCancelled = "export_item_cancelled";
    public const string ExportBatchCancelled = "export_batch_cancelled";
    public const string ReferenceEntered = "reference_entered";
    public const string MarkedPaid = "marked_paid";
    public const string ProcessCompleted = "process_completed";

    public static string DescriptionHe(string eventType) => eventType switch
    {
        ItemCreated => "נוצר פריט סיוע",
        Submitted => "הוגש לאישור",
        Resubmitted => "הוגש מחדש",
        Approved => "אושר",
        Rejected => "נדחה",
        Returned => "הוחזר לתיקון",
        Suspended => "מושהה",
        ItemEdited => "עריכת פריט",
        ExportBatchCreated => "נוצר גליון ייצוא",
        ExportItemCancelled => "בוטל ייצוא לפריט",
        ExportBatchCancelled => "בוטל גליון ייצוא",
        ReferenceEntered => "הוזנה אסמכתא",
        MarkedPaid => "סומן כשולם",
        ProcessCompleted => "תהליך הושלם",
        _ => throw new ArgumentException($"No approved Hebrew label for history event type '{eventType}'.")
    };
}
/// <summary>Closed editable allow-list (arch-plan §10). Unknown keys rejected.</summary>
public static class AssistanceItemEditableFields
{
    public const string AssistanceTypeId = "assistance_type_id";
    public const string Description = "description";
    public const string Amount = "amount";
    public const string SupplierId = "supplier_id";
    public const string PaymentTarget = "payment_target";
    public const string Beneficiary = "beneficiary"; // maps to PayeeName
    public const string PaymentMethod = "payment_method";
    public const string BankNumber = "bank_number";
    public const string BranchNumber = "branch_number";
    public const string AccountNumber = "account_number";
    public const string AccountHolderName = "account_holder_name"; // maps to PayeeName when other+transfer
    /// <summary>History-only key for status transitions (not in edit allow-list).</summary>
    public const string Status = "status";

    public static readonly HashSet<string> All =
    [
        AssistanceTypeId, Description, Amount, SupplierId, PaymentTarget, Beneficiary,
        PaymentMethod, BankNumber, BranchNumber, AccountNumber, AccountHolderName
    ];

    public static readonly HashSet<string> Sensitive =
    [
        BankNumber, BranchNumber, AccountNumber, AccountHolderName
    ];

    public static string LabelHe(string fieldKey) => fieldKey switch
    {
        AssistanceTypeId => "סוג סיוע",
        Description => "תיאור",
        Amount => "סכום לתשלום",
        SupplierId => "ספק",
        PaymentTarget => "יעד תשלום",
        Beneficiary => "מוטב",
        PaymentMethod => "אמצעי תשלום",
        BankNumber => "מספר בנק",
        BranchNumber => "מספר סניף",
        AccountNumber => "מספר חשבון",
        AccountHolderName => "שם בעל החשבון",
        Status => "סטטוס",
        _ => throw new ArgumentException($"No approved Hebrew label for history field '{fieldKey}'.")
    };
}
