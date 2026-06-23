using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Services;

/// <summary>
/// Maps scoped audit records to Home Dashboard activity labels and semantics.
/// </summary>
public static class HomeActivityPresentation
{
    public static bool IsDisplayableActivity(AuditLog log)
    {
        if (log.EntityType == "committee_decision")
        {
            if (log.Action == "create")
                return true;

            if (log.FieldName == "status" && !string.IsNullOrWhiteSpace(log.NewValue))
                return true;

            return log.Action is "submit" or "approve" or "reject" or "return_for_revision"
                or "suspend" or "resume" or "cancel";
        }

        if (log.EntityType == "payment_execution")
        {
            return log.FieldName == "status" && !string.IsNullOrWhiteSpace(log.NewValue);
        }

        return false;
    }

    public static (string StatusLabel, string StatusSemantic, string? WorkflowStatus) Resolve(AuditLog log)
    {
        if (log.EntityType == "committee_decision")
        {
            if (log.Action == "create")
                return ("נוצרה החלטה", HomeWorkflowStatus.Draft, CommitteeDecisionStatuses.Draft);

            var status = log.FieldName == "status" ? log.NewValue : StatusFromCommitteeAction(log.Action);
            if (string.IsNullOrWhiteSpace(status))
                return ("עדכון סטטוס", HomeWorkflowStatus.Draft, null);

            return (CommitteeStatusLabel(status), CommitteeStatusSemantic(status), status);
        }

        var paymentStatus = log.NewValue;
        if (string.IsNullOrWhiteSpace(paymentStatus))
            return ("עדכון תשלום", HomeWorkflowStatus.PendingExecution, null);

        return (PaymentStatusLabel(paymentStatus), PaymentStatusSemantic(paymentStatus), paymentStatus);
    }

    private static string? StatusFromCommitteeAction(string action) => action switch
    {
        "submit" => CommitteeDecisionStatuses.Submitted,
        "approve" => CommitteeDecisionStatuses.Approved,
        "reject" => CommitteeDecisionStatuses.Rejected,
        "return_for_revision" => CommitteeDecisionStatuses.ReturnedForRevision,
        "suspend" => CommitteeDecisionStatuses.Suspended,
        "cancel" => CommitteeDecisionStatuses.Cancelled,
        _ => null
    };

    public static string CommitteeStatusLabel(string status) => status switch
    {
        CommitteeDecisionStatuses.Draft => "טיוטה",
        CommitteeDecisionStatuses.Submitted => "ממתין לאישור",
        CommitteeDecisionStatuses.ReturnedForRevision => "הוחזר לטיפול",
        CommitteeDecisionStatuses.Approved => "אושר",
        CommitteeDecisionStatuses.Rejected => "נדחה",
        CommitteeDecisionStatuses.Suspended => "בהשהיה",
        CommitteeDecisionStatuses.Cancelled => "בוטל",
        CommitteeDecisionStatuses.PartiallyPaid => "שולם חלקית",
        CommitteeDecisionStatuses.FullyPaid => "שולם במלואו",
        _ => status
    };

    public static string CommitteeStatusSemantic(string status) => status switch
    {
        CommitteeDecisionStatuses.Draft => HomeWorkflowStatus.Draft,
        CommitteeDecisionStatuses.Submitted => HomeWorkflowStatus.PendingApproval,
        CommitteeDecisionStatuses.ReturnedForRevision => HomeWorkflowStatus.ReturnedForTreatment,
        CommitteeDecisionStatuses.Approved => HomeWorkflowStatus.Paid,
        CommitteeDecisionStatuses.Rejected => HomeWorkflowStatus.Rejected,
        CommitteeDecisionStatuses.Suspended => HomeWorkflowStatus.OnHold,
        CommitteeDecisionStatuses.Cancelled => HomeWorkflowStatus.Rejected,
        CommitteeDecisionStatuses.PartiallyPaid => HomeWorkflowStatus.Paid,
        CommitteeDecisionStatuses.FullyPaid => HomeWorkflowStatus.Paid,
        _ => HomeWorkflowStatus.Draft
    };

    public static string PaymentStatusLabel(string status) => status switch
    {
        PaymentExecutionStatuses.AwaitingPayment => "ממתין לביצוע",
        PaymentExecutionStatuses.Executing => "בביצוע",
        PaymentExecutionStatuses.ProofUploaded => "הוכחה הועלתה",
        PaymentExecutionStatuses.Paid => "שולם",
        PaymentExecutionStatuses.ReturnedToCoordinator => "הוחזר לטיפול",
        PaymentExecutionStatuses.OnHold => "בהשהיה",
        _ => status
    };

    public static string PaymentStatusSemantic(string status) => status switch
    {
        PaymentExecutionStatuses.AwaitingPayment => HomeWorkflowStatus.PendingExecution,
        PaymentExecutionStatuses.Executing => HomeWorkflowStatus.PendingExecution,
        PaymentExecutionStatuses.ProofUploaded => HomeWorkflowStatus.PendingExecution,
        PaymentExecutionStatuses.Paid => HomeWorkflowStatus.Paid,
        PaymentExecutionStatuses.ReturnedToCoordinator => HomeWorkflowStatus.ReturnedForTreatment,
        PaymentExecutionStatuses.OnHold => HomeWorkflowStatus.OnHold,
        _ => HomeWorkflowStatus.PendingExecution
    };
}
