namespace FamilyAssistance.Api.Constants;

/// <summary>
/// Semantic workflow status identifiers for Home Dashboard widgets.
/// Presentation (colors, icons) is mapped on the frontend only.
/// </summary>
public static class HomeWorkflowStatus
{
    public const string Draft = "draft";
    public const string PendingApproval = "pending_approval";
    public const string ReturnedForTreatment = "returned_for_treatment";
    public const string OnHold = "on_hold";
    public const string PendingExecution = "pending_execution";
    public const string Paid = "paid";
    public const string Rejected = "rejected";
}
