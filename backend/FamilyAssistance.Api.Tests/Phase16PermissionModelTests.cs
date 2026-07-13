using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Tests;

/// <summary>Phase 16 M93 — export-batch permission model + C10 alignment.</summary>
public sealed class Phase16PermissionModelTests
{
    [Fact]
    public void CatalogSeed_IncludesPhase16ExportKeys_DistinctCreateAndDownload()
    {
        var keys = PermissionCatalogSeed.Rows.Select(r => r.PermissionKey).ToHashSet();

        Assert.Contains(PermissionKeys.PaymentsExportBatchesCreate, keys);
        Assert.Contains(PermissionKeys.PaymentsExportBatchesDownload, keys);
        Assert.Contains(PermissionKeys.PaymentsExportBatchesCancel, keys);
        Assert.Contains(PermissionKeys.PaymentsExportBatchItemsCancel, keys);
        Assert.Contains(PermissionKeys.PaymentsEditAssistanceItems, keys);
        Assert.Contains(PermissionKeys.PaymentsEnterReference, keys);
        Assert.Contains(PermissionKeys.AssistanceItemsViewHistory, keys);
        Assert.Contains(PermissionKeys.AssistanceItemsComplete, keys);

        var create = PermissionCatalogSeed.Rows.Single(r => r.PermissionKey == PermissionKeys.PaymentsExportBatchesCreate);
        var download = PermissionCatalogSeed.Rows.Single(r => r.PermissionKey == PermissionKeys.PaymentsExportBatchesDownload);
        Assert.NotEqual(create.PermissionKey, download.PermissionKey);
        Assert.Equal("payments", create.Category);
        Assert.Equal("payments", download.Category);
    }

    [Fact]
    public void FinanceTemplate_GrantsCreateAndDownloadTogether_AsDistinctKeys()
    {
        var grants = RoleTemplateSeed.GrantsForPreset(FactoryPresetKeys.Finance)
            .Select(g => g.PermissionKey)
            .ToHashSet();

        Assert.Contains(PermissionKeys.PaymentsView, grants);
        Assert.Contains(PermissionKeys.PaymentsExportBatchesCreate, grants);
        Assert.Contains(PermissionKeys.PaymentsExportBatchesDownload, grants);
        Assert.Contains(PermissionKeys.PaymentsExportBatchesCancel, grants);
        Assert.Contains(PermissionKeys.PaymentsExportBatchItemsCancel, grants);
        Assert.Contains(PermissionKeys.PaymentsEditAssistanceItems, grants);
        Assert.Contains(PermissionKeys.PaymentsEnterReference, grants);
        Assert.Contains(PermissionKeys.AssistanceItemsViewHistory, grants);
        Assert.Contains(PermissionKeys.AssistanceItemsComplete, grants);
        // Legacy proof path retained
        Assert.Contains(PermissionKeys.PaymentsExecute, grants);
    }

    [Fact]
    public void PaymentsView_DoesNotImplyEditAssistanceItems()
    {
        var auth = AuthWith(PermissionKeys.PaymentsView);

        Assert.True(PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsView));
        Assert.False(PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsEditAssistanceItems));
        Assert.False(PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesCreate));
        Assert.False(PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesDownload));
    }

    [Fact]
    public void CreateDoesNotImplyDownload_AndDownloadDoesNotImplyCreate()
    {
        var createOnly = AuthWith(PermissionKeys.PaymentsExportBatchesCreate);
        var downloadOnly = AuthWith(PermissionKeys.PaymentsExportBatchesDownload);

        Assert.True(PermissionService.HasWorkflowGrant(createOnly, PermissionKeys.PaymentsExportBatchesCreate));
        Assert.False(PermissionService.HasWorkflowGrant(createOnly, PermissionKeys.PaymentsExportBatchesDownload));

        Assert.True(PermissionService.HasWorkflowGrant(downloadOnly, PermissionKeys.PaymentsExportBatchesDownload));
        Assert.False(PermissionService.HasWorkflowGrant(downloadOnly, PermissionKeys.PaymentsExportBatchesCreate));
    }

    [Fact]
    public void C10_Decisions_NoLongerExposeSendToExecution_OrEnterReference()
    {
        var decision = new CommitteeDecision
        {
            Id = Guid.NewGuid(),
            Status = CommitteeDecisionStatuses.Approved,
            CreatedByUserId = Guid.NewGuid(),
        };
        var approved = new AssistanceItem
        {
            Id = Guid.NewGuid(),
            Status = AssistanceItemStatuses.Approved,
            CommitteeDecision = decision,
        };
        var waiting = new AssistanceItem
        {
            Id = Guid.NewGuid(),
            Status = AssistanceItemStatuses.WaitingForReference,
            CommitteeDecision = decision,
        };

        var auth = AuthWith(
            PermissionKeys.PaymentsExportBatchesCreate,
            PermissionKeys.PaymentsEnterReference,
            PermissionKeys.AssistanceItemsComplete);

        Assert.DoesNotContain("send_to_execution", WorkflowHelpers.AvailableAssistanceItemActions(approved, decision, auth));
        Assert.DoesNotContain("enter_reference", WorkflowHelpers.AvailableAssistanceItemActions(waiting, decision, auth));
    }

    [Fact]
    public void PaymentRowActions_ExposeEnterReference_OnWaitingForReference()
    {
        var item = new AssistanceItem
        {
            Id = Guid.NewGuid(),
            Status = AssistanceItemStatuses.WaitingForReference,
        };
        var auth = AuthWith(PermissionKeys.PaymentsEnterReference);
        var actions = WorkflowHelpers.AvailablePaymentRowActions(item, null, auth);
        Assert.Contains("enter_reference", actions);
        Assert.DoesNotContain("edit", actions);
    }

    [Fact]
    public void PaymentRowActions_ExposeEdit_WhenEditableAndNoActiveExport()
    {
        var item = new AssistanceItem
        {
            Id = Guid.NewGuid(),
            Status = AssistanceItemStatuses.Approved,
        };
        var withEdit = AuthWith(PermissionKeys.PaymentsEditAssistanceItems);
        var actions = WorkflowHelpers.AvailablePaymentRowActions(item, null, withEdit);
        Assert.Contains("edit", actions);
        Assert.DoesNotContain("adjust_amount", actions);
    }

    [Fact]
    public void C10_SendToExecution_RequiresExportBatchesCreate_NotLegacyExecute()
    {
        // Retained for historical naming: create permission is required for batch export eligibility.
        var item = new AssistanceItem { Id = Guid.NewGuid(), Status = AssistanceItemStatuses.Approved };
        var withCreate = AuthWith(PermissionKeys.PaymentsExportBatchesCreate);
        var withLegacyExecuteOnly = AuthWith(PermissionKeys.PaymentsExecute);

        Assert.True(WorkflowHelpers.IsEligibleForExport(item, hasActiveExportItem: false, withCreate));
        Assert.False(WorkflowHelpers.IsEligibleForExport(item, hasActiveExportItem: false, withLegacyExecuteOnly));
    }

    [Fact]
    public void LegacyExecute_StillGatesPaymentExecutionExecuteAction()
    {
        var payment = new PaymentExecution
        {
            Id = Guid.NewGuid(),
            Status = PaymentExecutionStatuses.AwaitingPayment,
            CommitteeDecision = new CommitteeDecision { Status = CommitteeDecisionStatuses.Approved },
        };

        var withExecute = AuthWith(PermissionKeys.PaymentsExecute);
        var withCreateOnly = AuthWith(PermissionKeys.PaymentsExportBatchesCreate);

        Assert.Contains("execute", WorkflowHelpers.AvailablePaymentActions(payment, withExecute));
        Assert.DoesNotContain("execute", WorkflowHelpers.AvailablePaymentActions(payment, withCreateOnly));
    }

    [Fact]
    public void NoRoleNameAuthorization_InPermissionServiceHasWorkflowGrant()
    {
        // Coordinator system role string alone does not grant export create.
        var auth = new AuthorizationContext
        {
            UserId = Guid.NewGuid(),
            SystemRole = Roles.Coordinator,
            OrganizationId = Guid.NewGuid(),
            Grants = [],
        };

        Assert.False(PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesCreate));
        Assert.False(PermissionService.HasWorkflowGrant(auth, PermissionKeys.PaymentsExportBatchesDownload));
    }

    private static AuthorizationContext AuthWith(params string[] keys) =>
        new()
        {
            UserId = Guid.NewGuid(),
            SystemRole = Roles.Coordinator,
            OrganizationId = Guid.NewGuid(),
            Grants = keys
                .Select(k => new GrantContext { PermissionKey = k, Scope = PermissionScopes.Organization })
                .ToList(),
        };
}
