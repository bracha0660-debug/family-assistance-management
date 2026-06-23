using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Services;

public static class WorkflowSectionRegistry
{
    public sealed record SectionDef(
        string SectionId,
        string Title,
        string Visibility,
        string Kind,
        Func<AuthorizationContext, bool> IsVisible,
        Func<AuthorizationContext, bool> HasActionGrant);

    public static readonly IReadOnlyList<SectionDef> DecisionSections =
    [
        new("my_drafts", "טיוטות שלי", "mine", "decision",
            auth => HasView(auth) && (HasGrant(auth, PermissionKeys.CommitteeDecisionsCreate)
                || HasGrant(auth, PermissionKeys.CommitteeDecisionsEditDraft)),
            auth => PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsSubmit)),
        new("my_returned_for_revision", "שלי — הוחזר לתיקון", "mine", "decision",
            auth => HasView(auth) && HasGrant(auth, PermissionKeys.CommitteeDecisionsEditDraft),
            auth => PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsEditDraft)),
        new("my_waiting_manager_approval", "שלי — ממתין לאישור מנהל", "mine", "decision",
            auth => HasView(auth) && (HasGrant(auth, PermissionKeys.CommitteeDecisionsCreate)
                || HasGrant(auth, PermissionKeys.CommitteeDecisionsSubmit)),
            _ => false),
        new("my_suspended", "שלי — מושעה (בהמתנה)", "mine", "decision",
            auth => HasView(auth) && (HasGrant(auth, PermissionKeys.CommitteeDecisionsCreate)
                || HasGrant(auth, PermissionKeys.CommitteeDecisionsSubmit)),
            _ => false),
        new("my_in_finance_execution", "שלי — בביצוע כספי", "mine", "decision",
            auth => HasView(auth),
            _ => false),
        new("my_paid_completed", "שלי — שולם / הושלם", "mine", "decision",
            auth => HasView(auth),
            _ => false),
        new("my_rejected", "שלי — נדחה / בוטל", "mine", "decision",
            auth => HasView(auth) && (HasGrant(auth, PermissionKeys.CommitteeDecisionsCreate)
                || HasGrant(auth, PermissionKeys.CommitteeDecisionsSubmit)),
            _ => false),
        new("waiting_my_approval", "ממתין לאישורי", "org", "decision",
            auth => HasView(auth) && HasGrant(auth, PermissionKeys.CommitteeDecisionsApprove),
            auth => PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsApprove)),
        new("approved", "אושרו", "org", "decision",
            auth => HasView(auth) && HasGrant(auth, PermissionKeys.CommitteeDecisionsApprove),
            auth => PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsApprove)),
        new("manager_rejected", "נדחו", "org", "decision",
            auth => HasView(auth) && HasGrant(auth, PermissionKeys.CommitteeDecisionsApprove),
            _ => false),
        new("manager_returned", "הוחזרו לתיקון", "org", "decision",
            auth => HasView(auth) && HasGrant(auth, PermissionKeys.CommitteeDecisionsApprove),
            _ => false),
        new("manager_suspended", "מושעים (בהמתנה)", "org", "decision",
            auth => HasView(auth) && HasGrant(auth, PermissionKeys.CommitteeDecisionsApprove),
            auth => PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsApprove)),
    ];

    public static readonly IReadOnlyList<SectionDef> PaymentSections =
    [
        new("finance_on_hold", "מושעים — לא ניתן לביצוע", "org", "payment",
            auth => HasGrant(auth, PermissionKeys.PaymentsView),
            _ => false),
        new("finance_awaiting_execution", "ממתין לביצוע", "org", "payment",
            auth => HasGrant(auth, PermissionKeys.PaymentsView),
            auth => PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExecute)),
        new("finance_executing", "בביצוע", "org", "payment",
            auth => HasGrant(auth, PermissionKeys.PaymentsView),
            auth => PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsUploadProof)),
        new("finance_proof_uploaded", "הוכחה הועלתה — ממתין לסימון שולם", "org", "payment",
            auth => HasGrant(auth, PermissionKeys.PaymentsView),
            auth => PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsMarkPaid)),
        new("finance_paid", "שולם", "org", "payment",
            auth => HasGrant(auth, PermissionKeys.PaymentsView),
            _ => false),
        new("finance_returned", "הוחזר לרכז", "org", "payment",
            auth => HasGrant(auth, PermissionKeys.PaymentsView),
            _ => false),
    ];

    public static IEnumerable<SectionDef> VisibleDecisionSections(AuthorizationContext auth) =>
        DecisionSections.Where(s => s.IsVisible(auth));

    public static IEnumerable<SectionDef> VisiblePaymentSections(AuthorizationContext auth) =>
        PaymentSections.Where(s => s.IsVisible(auth));

    public static bool MatchesDecisionSection(CommitteeDecision decision, string sectionId, Guid userId)
    {
        return sectionId switch
        {
            "my_drafts" => IsOwned(decision, userId) && decision.Status == CommitteeDecisionStatuses.Draft,
            "my_returned_for_revision" => IsOwned(decision, userId)
                && decision.Status == CommitteeDecisionStatuses.ReturnedForRevision,
            "my_waiting_manager_approval" => IsOwned(decision, userId)
                && decision.Status == CommitteeDecisionStatuses.Submitted,
            "my_suspended" => IsOwned(decision, userId)
                && decision.Status == CommitteeDecisionStatuses.Suspended,
            "my_in_finance_execution" => IsOwned(decision, userId)
                && decision.Status is CommitteeDecisionStatuses.Approved or CommitteeDecisionStatuses.PartiallyPaid,
            "my_paid_completed" => IsOwned(decision, userId)
                && decision.Status == CommitteeDecisionStatuses.FullyPaid,
            "my_rejected" => IsOwned(decision, userId)
                && decision.Status is CommitteeDecisionStatuses.Rejected or CommitteeDecisionStatuses.Cancelled,
            "waiting_my_approval" => decision.Status == CommitteeDecisionStatuses.Submitted,
            "approved" => decision.Status is CommitteeDecisionStatuses.Approved
                or CommitteeDecisionStatuses.PartiallyPaid,
            "manager_rejected" => decision.Status == CommitteeDecisionStatuses.Rejected,
            "manager_returned" => decision.Status == CommitteeDecisionStatuses.ReturnedForRevision,
            "manager_suspended" => decision.Status == CommitteeDecisionStatuses.Suspended,
            _ => false
        };
    }

    public static bool IsDecisionSectionOrgScoped(string sectionId) =>
        sectionId.StartsWith("waiting_", StringComparison.Ordinal)
        || sectionId.StartsWith("manager_", StringComparison.Ordinal)
        || sectionId is "approved";

    public static int CountActionableDecisions(
        IEnumerable<CommitteeDecision> decisions,
        SectionDef section,
        AuthorizationContext auth)
    {
        if (!section.HasActionGrant(auth))
            return 0;

        return section.SectionId switch
        {
            "waiting_my_approval" => decisions.Count(d => d.Status == CommitteeDecisionStatuses.Submitted),
            "manager_suspended" => decisions.Count(d => d.Status == CommitteeDecisionStatuses.Suspended),
            "approved" => decisions.Count(d => d.Status is CommitteeDecisionStatuses.Approved
                or CommitteeDecisionStatuses.PartiallyPaid),
            "my_returned_for_revision" => decisions.Count(d =>
                WorkflowHelpers.IsDecisionOwnedByUser(d, auth.UserId)
                && d.Status == CommitteeDecisionStatuses.ReturnedForRevision),
            "my_drafts" => decisions.Count(d =>
                WorkflowHelpers.IsDecisionOwnedByUser(d, auth.UserId)
                && d.Status == CommitteeDecisionStatuses.Draft
                && d.Items.Count > 0),
            _ => 0
        };
    }

    public static bool MatchesPaymentSection(PaymentExecution payment, string sectionId)
    {
        var decision = payment.CommitteeDecision;
        return sectionId switch
        {
            "finance_on_hold" => payment.Status == PaymentExecutionStatuses.OnHold
                || decision?.Status == CommitteeDecisionStatuses.Suspended,
            "finance_awaiting_execution" => payment.Status == PaymentExecutionStatuses.AwaitingPayment
                && decision?.Status is CommitteeDecisionStatuses.Approved or CommitteeDecisionStatuses.PartiallyPaid,
            "finance_executing" => payment.Status == PaymentExecutionStatuses.Executing,
            "finance_proof_uploaded" => payment.Status == PaymentExecutionStatuses.ProofUploaded,
            "finance_paid" => payment.Status == PaymentExecutionStatuses.Paid,
            "finance_returned" => payment.Status == PaymentExecutionStatuses.ReturnedToCoordinator,
            _ => false
        };
    }

    public static int CountActionablePayments(
        IEnumerable<PaymentExecution> payments,
        SectionDef section,
        AuthorizationContext auth)
    {
        if (!section.HasActionGrant(auth))
            return 0;

        return section.SectionId switch
        {
            "finance_awaiting_execution" => payments.Count(p =>
                p.Status == PaymentExecutionStatuses.AwaitingPayment),
            "finance_executing" => payments.Count(p => p.Status == PaymentExecutionStatuses.Executing),
            "finance_proof_uploaded" => payments.Count(p =>
                p.Status == PaymentExecutionStatuses.ProofUploaded),
            _ => 0
        };
    }

    private static bool IsOwned(CommitteeDecision decision, Guid userId) =>
        WorkflowHelpers.IsDecisionOwnedByUser(decision, userId);

    private static bool HasView(AuthorizationContext auth) =>
        auth.FullOrgAccess || auth.HasGrant(PermissionKeys.CommitteeDecisionsView);

    private static bool HasGrant(AuthorizationContext auth, string key) =>
        auth.FullOrgAccess || auth.HasGrant(key);
}
