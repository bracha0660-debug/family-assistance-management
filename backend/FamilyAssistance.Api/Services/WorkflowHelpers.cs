using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Services;

public static class WorkflowHelpers
{
    public static bool IsFamilyOwnedByUser(Family family, Guid userId) =>
        family.AssignedCoordinatorId == userId;

    /// <summary>Phase 14 G1 — draft ownership is creator, not family coordinator.</summary>
    public static bool IsDecisionOwnedByUser(CommitteeDecision decision, Guid userId) =>
        decision.CreatedByUserId == userId;

    /// <summary>
    /// Phase 16.3 — owner or SuperAdmin acting in org may perform owner-scoped draft actions.
    /// Does not use FullOrgAccess (OrganizationAdministrator stays owner-scoped).
    /// </summary>
    public static bool CanActAsDecisionOwner(CommitteeDecision decision, AuthorizationContext auth) =>
        IsDecisionOwnedByUser(decision, auth.UserId) || auth.IsSuperAdminInOrganization;

    public static IQueryable<CommitteeDecision> ApplyOwnershipMine(
        IQueryable<CommitteeDecision> query,
        Guid userId) =>
        query.Where(d => d.CreatedByUserId == userId);

    public static IQueryable<Family> ApplyOwnershipMine(IQueryable<Family> query, Guid userId) =>
        query.Where(f => f.AssignedCoordinatorId == userId);

    public static IQueryable<AssistanceItem> ApplyItemOwnershipMine(
        IQueryable<AssistanceItem> query,
        Guid userId) =>
        query.Where(i => i.CommitteeDecision!.CreatedByUserId == userId);

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
            i.Status is AssistanceItemStatuses.WaitingForReference or AssistanceItemStatuses.Approved
            || PaymentExecutionStatuses.ActiveQueue.Contains(i.ExecutionStatus));
    }

    /// <summary>
    /// G14 — derived aggregate for display; does not gate item availableActions.
    /// </summary>
    public static string ComputeDerivedDecisionStatus(CommitteeDecision decision)
    {
        if (decision.Status is CommitteeDecisionStatuses.Draft or CommitteeDecisionStatuses.Cancelled)
            return decision.Status;

        var items = decision.Items;
        if (items.Count == 0)
            return decision.Status;

        var statuses = items.Select(i => i.Status).ToList();
        if (statuses.All(s => s == AssistanceItemStatuses.Draft))
            return CommitteeDecisionStatuses.Draft;

        if (statuses.All(s => s == AssistanceItemStatuses.Suspended))
            return CommitteeDecisionStatuses.Suspended;

        if (statuses.All(s => s is AssistanceItemStatuses.Rejected))
            return CommitteeDecisionStatuses.Rejected;

        if (statuses.All(s => s is AssistanceItemStatuses.Paid or AssistanceItemStatuses.Completed))
            return CommitteeDecisionStatuses.FullyPaid;

        var hasPaid = statuses.Any(s => s is AssistanceItemStatuses.Paid or AssistanceItemStatuses.Completed);
        var hasPaymentStage = statuses.Any(s =>
            s is AssistanceItemStatuses.WaitingForReference
                or AssistanceItemStatuses.Approved
                or AssistanceItemStatuses.Paid
                or AssistanceItemStatuses.Completed);

        if (hasPaid && hasPaymentStage && !statuses.All(s => s is AssistanceItemStatuses.Paid or AssistanceItemStatuses.Completed))
            return CommitteeDecisionStatuses.PartiallyPaid;

        if (statuses.Any(s => s == AssistanceItemStatuses.Returned)
            && !statuses.Any(s => s is AssistanceItemStatuses.Submitted
                or AssistanceItemStatuses.Approved
                or AssistanceItemStatuses.WaitingForReference
                or AssistanceItemStatuses.Paid
                or AssistanceItemStatuses.Completed))
            return CommitteeDecisionStatuses.ReturnedForRevision;

        if (statuses.Any(s => s == AssistanceItemStatuses.Submitted)
            && !statuses.Any(s => s is AssistanceItemStatuses.Approved
                or AssistanceItemStatuses.WaitingForReference
                or AssistanceItemStatuses.Paid
                or AssistanceItemStatuses.Completed))
            return CommitteeDecisionStatuses.Submitted;

        if (statuses.Any(s => s is AssistanceItemStatuses.Approved
                or AssistanceItemStatuses.WaitingForReference
                or AssistanceItemStatuses.Paid
                or AssistanceItemStatuses.Completed))
            return hasPaid ? CommitteeDecisionStatuses.PartiallyPaid : CommitteeDecisionStatuses.Approved;

        return CommitteeDecisionStatuses.Submitted;
    }

    public static IReadOnlyList<string> AvailableDecisionActions(CommitteeDecision decision, AuthorizationContext auth)
    {
        var actions = new List<string>();
        var effectiveOwned = CanActAsDecisionOwner(decision, auth);

        // Post-submit decision transitions are deprecated (item-level). Keep draft/cancel only.
        if (decision.Status is CommitteeDecisionStatuses.Draft or CommitteeDecisionStatuses.ReturnedForRevision)
        {
            if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsEditDraft) && effectiveOwned)
                actions.Add("edit");
            if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsSubmit) && effectiveOwned
                && decision.Items.Count > 0)
                actions.Add("submit");
        }

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

    public static IReadOnlyList<string> AvailableAssistanceItemActions(
        AssistanceItem item,
        CommitteeDecision parent,
        AuthorizationContext auth)
    {
        var actions = new List<string>();
        var effectiveOwned = CanActAsDecisionOwner(parent, auth);

        switch (item.Status)
        {
            case AssistanceItemStatuses.Submitted:
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsApprove))
                {
                    actions.Add("approve");
                    actions.Add("suspend");
                }
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsReject))
                {
                    actions.Add("reject");
                    actions.Add("return");
                }
                break;

            case AssistanceItemStatuses.Returned:
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.AssistanceItemsEdit) && effectiveOwned)
                    actions.Add("edit");
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsSubmit) && effectiveOwned)
                    actions.Add("resubmit");
                break;

            case AssistanceItemStatuses.Approved:
                // Phase 16: payment execution (send/export) lives on PaymentsQueuePage, not Decisions.
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsApprove))
                    actions.Add("suspend");
                break;

            case AssistanceItemStatuses.Suspended:
                // Suspended Recovery: exit via existing approve/reject/return only (no restore/unsuspend/resume).
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsApprove))
                    actions.Add("approve");
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.CommitteeDecisionsReject))
                {
                    actions.Add("reject");
                    actions.Add("return");
                }
                break;

            case AssistanceItemStatuses.WaitingForReference:
                // enter_reference moved to Payments operational surface (M94).
                break;

            case AssistanceItemStatuses.Paid:
                if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.AssistanceItemsComplete))
                    actions.Add("complete");
                break;
        }

        if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.AssistanceItemsViewHistory)
            || auth.FullOrgAccess)
        {
            actions.Add("view_history");
        }

        return actions;
    }

    /// <summary>PaymentsQueuePage / payment-rows availableActions (Phase 16 + B).</summary>
    public static IReadOnlyList<string> AvailablePaymentRowActions(
        AssistanceItem item,
        ExportBatchItem? activeExportItem,
        AuthorizationContext auth)
    {
        var actions = new List<string>();
        var hasReference = !string.IsNullOrWhiteSpace(item.ExecutionReference)
            || !string.IsNullOrWhiteSpace(item.PaymentExecution?.ExecutionReference);
        var hasActiveExport = activeExportItem is not null
            && activeExportItem.Status == ExportBatchItemStatuses.Active;

        if (item.Status is AssistanceItemStatuses.Approved or AssistanceItemStatuses.WaitingForReference
            && !hasReference
            && !hasActiveExport
            && item.Status is not (AssistanceItemStatuses.Paid or AssistanceItemStatuses.Completed)
            && PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsEditAssistanceItems))
        {
            actions.Add("edit");
        }

        if (item.Status == AssistanceItemStatuses.WaitingForReference
            && PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsEnterReference))
        {
            actions.Add("enter_reference");
        }

        if (hasActiveExport
            && item.Status == AssistanceItemStatuses.WaitingForReference
            && !hasReference
            && PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchItemsCancel))
        {
            actions.Add("cancel_export_item");
        }

        if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.AssistanceItemsViewHistory)
            || auth.FullOrgAccess)
        {
            actions.Add("view_history");
        }

        return actions;
    }

    public static IReadOnlyList<string> AvailableExportBatchActions(ExportBatch batch, AuthorizationContext auth)
    {
        var actions = new List<string>();
        if (PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesDownload)
            && !string.IsNullOrWhiteSpace(batch.StoredFileName))
        {
            actions.Add("download");
        }

        if (batch.Status is ExportBatchStatuses.Open or ExportBatchStatuses.PartiallyCancelled
            && batch.ActiveItemCount > 0
            && PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesCancel))
        {
            actions.Add("cancel_batch");
        }

        return actions;
    }

    public static bool IsEligibleForExport(AssistanceItem item, bool hasActiveExportItem, AuthorizationContext auth)
    {
        if (!PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesCreate))
            return false;
        if (item.Status != AssistanceItemStatuses.Approved)
            return false;
        if (hasActiveExportItem)
            return false;
        if (item.Status is AssistanceItemStatuses.WaitingForReference
            or AssistanceItemStatuses.Paid
            or AssistanceItemStatuses.Completed)
            return false;
        return true;
    }

    public static IReadOnlyList<string> AvailablePaymentActions(PaymentExecution payment, AuthorizationContext auth)
    {
        if (payment.Status == PaymentExecutionStatuses.OnHold
            || payment.CommitteeDecision?.Status == CommitteeDecisionStatuses.Suspended)
            return [];

        // New export-batch path uses payment-row enter_reference; legacy proof/mark_paid retained.
        if (payment.Status == PaymentExecutionStatuses.WaitingForReference)
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
