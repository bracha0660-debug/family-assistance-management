using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Services;

public static class WorkflowHelpers
{
    public static bool IsFamilyOwnedByUser(Family family, Guid userId) =>
        family.AssignedCoordinatorId == userId;

    public static bool IsDecisionOwnedByUser(CommitteeDecision decision, Guid userId) =>
        decision.Family is not null && decision.Family.AssignedCoordinatorId == userId;

    public static IQueryable<CommitteeDecision> ApplyOwnershipMine(
        IQueryable<CommitteeDecision> query,
        Guid userId) =>
        query.Where(d => d.Family!.AssignedCoordinatorId == userId);

    public static IQueryable<Family> ApplyOwnershipMine(IQueryable<Family> query, Guid userId) =>
        query.Where(f => f.AssignedCoordinatorId == userId);

    public static string ComputeWorkflowPhase(CommitteeDecision decision, IReadOnlyList<PaymentExecution>? payments = null)
    {
        return decision.Status switch
        {
            CommitteeDecisionStatuses.Draft or CommitteeDecisionStatuses.ReturnedForRevision => WorkflowPhases.Intake,
            CommitteeDecisionStatuses.Submitted => WorkflowPhases.PendingManager,
            CommitteeDecisionStatuses.Suspended => WorkflowPhases.OnHold,
            CommitteeDecisionStatuses.FullyPaid => WorkflowPhases.Completed,
            CommitteeDecisionStatuses.Rejected or CommitteeDecisionStatuses.Cancelled => WorkflowPhases.Closed,
            CommitteeDecisionStatuses.Approved or CommitteeDecisionStatuses.PartiallyPaid => HasActiveFinanceItems(decision, payments)
                ? WorkflowPhases.PendingFinance
                : WorkflowPhases.PendingFinance,
            _ => WorkflowPhases.Intake
        };
    }

    private static bool HasActiveFinanceItems(CommitteeDecision decision, IReadOnlyList<PaymentExecution>? payments)
    {
        if (payments is not null && payments.Count > 0)
        {
            return payments.Any(p =>
                PaymentExecutionStatuses.ActiveQueue.Contains(p.Status));
        }

        return decision.Items.Any(i =>
            PaymentExecutionStatuses.ActiveQueue.Contains(i.ExecutionStatus));
    }

    public static IReadOnlyList<string> AvailableDecisionActions(CommitteeDecision decision, AuthorizationContext auth)
    {
        var actions = new List<string>();
        var owned = decision.Family is not null && IsDecisionOwnedByUser(decision, auth.UserId);
        var canWorkflow = PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsApprove);

        if (decision.Status is CommitteeDecisionStatuses.Draft or CommitteeDecisionStatuses.ReturnedForRevision)
        {
            if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsEditDraft) && owned)
                actions.Add("edit");
            if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsSubmit) && owned
                && decision.Items.Count > 0)
                actions.Add("submit");
        }

        if (decision.Status == CommitteeDecisionStatuses.Submitted)
        {
            if (canWorkflow)
            {
                actions.Add("approve");
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsReject))
                {
                    actions.Add("reject");
                    actions.Add("return");
                }
                actions.Add("suspend");
            }
        }

        if (decision.Status is CommitteeDecisionStatuses.Approved or CommitteeDecisionStatuses.PartiallyPaid && canWorkflow)
            actions.Add("suspend");

        if (decision.Status == CommitteeDecisionStatuses.Suspended && canWorkflow)
            actions.Add("resume");

        if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsCancel))
        {
            var cancellable = new HashSet<string>
            {
                CommitteeDecisionStatuses.Draft,
                CommitteeDecisionStatuses.Submitted,
                CommitteeDecisionStatuses.ReturnedForRevision,
                CommitteeDecisionStatuses.Approved
            };
            if (cancellable.Contains(decision.Status))
                actions.Add("cancel");
        }

        return actions;
    }

    public static IReadOnlyList<string> AvailablePaymentActions(PaymentExecution payment, AuthorizationContext auth)
    {
        if (payment.Status == PaymentExecutionStatuses.OnHold
            || payment.CommitteeDecision?.Status == CommitteeDecisionStatuses.Suspended)
            return [];

        var actions = new List<string>();
        if (payment.Status == PaymentExecutionStatuses.AwaitingPayment
            && PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExecute))
            actions.Add("execute");
        if (payment.Status == PaymentExecutionStatuses.Executing
            && PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsUploadProof))
            actions.Add("upload_proof");
        if (payment.Status == PaymentExecutionStatuses.ProofUploaded
            && PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsMarkPaid))
            actions.Add("mark_paid");
        if (payment.Status is PaymentExecutionStatuses.AwaitingPayment
                or PaymentExecutionStatuses.Executing
                or PaymentExecutionStatuses.ProofUploaded
            && PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsReturnToCoordinator))
            actions.Add("return_to_coordinator");

        return actions;
    }
}
