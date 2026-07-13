namespace FamilyAssistance.Api.Constants;

public static class PermissionKeys
{
    public const string FamiliesView = "families.view";
    public const string FamiliesCreate = "families.create";
    public const string FamiliesEdit = "families.edit";
    public const string FamiliesDeactivate = "families.deactivate";
    public const string FamiliesRestore = "families.restore";
    public const string FamiliesExport = "families.export";

    public const string SuppliersView = "suppliers.view";
    public const string SuppliersCreate = "suppliers.create";
    public const string SuppliersEdit = "suppliers.edit";
    public const string SuppliersDeactivate = "suppliers.deactivate";
    public const string SuppliersRestore = "suppliers.restore";
    public const string SuppliersExport = "suppliers.export";

    public const string AssistanceTypesView = "assistance_types.view";
    public const string AssistanceTypesCreate = "assistance_types.create";
    public const string AssistanceTypesEdit = "assistance_types.edit";
    public const string AssistanceTypesDeactivate = "assistance_types.deactivate";
    public const string AssistanceTypesRestore = "assistance_types.restore";

    public const string CommitteeDecisionsView = "committee_decisions.view";
    public const string CommitteeDecisionsCreate = "committee_decisions.create";
    public const string CommitteeDecisionsEditDraft = "committee_decisions.edit_draft";
    public const string CommitteeDecisionsSubmit = "committee_decisions.submit";
    public const string CommitteeDecisionsApprove = "committee_decisions.approve";
    public const string CommitteeDecisionsReject = "committee_decisions.reject";
    public const string CommitteeDecisionsCancel = "committee_decisions.cancel";

    public const string AssistanceItemsView = "assistance_items.view";
    public const string AssistanceItemsCreate = "assistance_items.create";
    public const string AssistanceItemsEdit = "assistance_items.edit";
    public const string AssistanceItemsRemoveDraft = "assistance_items.remove_draft";
    public const string AssistanceItemsComplete = "assistance_items.complete";
    public const string AssistanceItemsViewHistory = "assistance_items.view_history";

    public const string PaymentsView = "payments.view";
    /// <summary>Legacy proof-path PE execute only. New export send uses <see cref="PaymentsExportBatchesCreate"/> (C10).</summary>
    public const string PaymentsExecute = "payments.execute";
    public const string PaymentsUploadProof = "payments.upload_proof";
    public const string PaymentsMarkPaid = "payments.mark_paid";
    public const string PaymentsReturnToCoordinator = "payments.return_to_coordinator";
    public const string PaymentsEnterReference = "payments.enter_reference";

    // Phase 16 export-batch permissions (distinct create vs download)
    public const string PaymentsExportBatchesCreate = "payments.export_batches.create";
    public const string PaymentsExportBatchesDownload = "payments.export_batches.download";
    public const string PaymentsExportBatchesCancel = "payments.export_batches.cancel";
    public const string PaymentsExportBatchItemsCancel = "payments.export_batch_items.cancel";
    public const string PaymentsEditAssistanceItems = "payments.edit_assistance_items";

    public static readonly string[] All =
    [
        FamiliesView, FamiliesCreate, FamiliesEdit, FamiliesDeactivate, FamiliesRestore, FamiliesExport,
        SuppliersView, SuppliersCreate, SuppliersEdit, SuppliersDeactivate, SuppliersRestore, SuppliersExport,
        AssistanceTypesView, AssistanceTypesCreate, AssistanceTypesEdit, AssistanceTypesDeactivate, AssistanceTypesRestore,
        CommitteeDecisionsView, CommitteeDecisionsCreate, CommitteeDecisionsEditDraft, CommitteeDecisionsSubmit,
        CommitteeDecisionsApprove, CommitteeDecisionsReject, CommitteeDecisionsCancel,
        AssistanceItemsView, AssistanceItemsCreate, AssistanceItemsEdit, AssistanceItemsRemoveDraft, AssistanceItemsComplete,
        AssistanceItemsViewHistory,
        PaymentsView, PaymentsExecute, PaymentsUploadProof, PaymentsMarkPaid, PaymentsReturnToCoordinator,
        PaymentsEnterReference,
        PaymentsExportBatchesCreate, PaymentsExportBatchesDownload, PaymentsExportBatchesCancel,
        PaymentsExportBatchItemsCancel, PaymentsEditAssistanceItems,
    ];

    public static readonly string[] OrganizationScopeOnly =
    [
        CommitteeDecisionsApprove,
        CommitteeDecisionsReject,
        PaymentsView,
        PaymentsExecute,
        PaymentsUploadProof,
        PaymentsMarkPaid,
        PaymentsReturnToCoordinator,
        PaymentsEnterReference,
        PaymentsExportBatchesCreate,
        PaymentsExportBatchesDownload,
        PaymentsExportBatchesCancel,
        PaymentsExportBatchItemsCancel,
        PaymentsEditAssistanceItems,
        AssistanceItemsComplete,
        AssistanceItemsViewHistory,
    ];

    public static readonly string[] BinaryCreateKeys =
    [
        FamiliesCreate,
        SuppliersCreate,
        AssistanceTypesCreate,
        CommitteeDecisionsCreate,
        AssistanceItemsCreate,
    ];

    public static bool IsUserOverrideAssignable(string permissionKey) =>
        !permissionKey.StartsWith("users.", StringComparison.Ordinal)
        && permissionKey != "activity_log.view";
}
