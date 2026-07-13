namespace FamilyAssistance.Api.Constants;

public static class RoleTemplateSeed
{
    public sealed record PresetDefinition(
        string FactoryPresetKey,
        string DefaultNameHe,
        string DescriptionHe,
        IReadOnlyList<(string PermissionKey, string Scope)> Grants);

    public static readonly IReadOnlyList<PresetDefinition> Presets =
    [
        new(
            FactoryPresetKeys.Coordinator,
            "רכז/ת",
            "תפקיד ברירת מחדל — נקודת התחלה לעבודה שטח",
            [
                (PermissionKeys.FamiliesView, PermissionScopes.MyRecords),
                (PermissionKeys.FamiliesCreate, PermissionScopes.Organization),
                (PermissionKeys.FamiliesEdit, PermissionScopes.MyRecords),
                (PermissionKeys.FamiliesDeactivate, PermissionScopes.MyRecords),
                (PermissionKeys.CommitteeDecisionsView, PermissionScopes.MyRecords),
                (PermissionKeys.CommitteeDecisionsCreate, PermissionScopes.Organization),
                (PermissionKeys.CommitteeDecisionsEditDraft, PermissionScopes.MyRecords),
                (PermissionKeys.CommitteeDecisionsSubmit, PermissionScopes.MyRecords),
                (PermissionKeys.AssistanceItemsView, PermissionScopes.MyRecords),
                (PermissionKeys.AssistanceItemsCreate, PermissionScopes.Organization),
                (PermissionKeys.AssistanceItemsEdit, PermissionScopes.MyRecords),
                (PermissionKeys.AssistanceItemsRemoveDraft, PermissionScopes.MyRecords),
                (PermissionKeys.AssistanceItemsViewHistory, PermissionScopes.MyRecords),
            ]),
        new(
            FactoryPresetKeys.Manager,
            "מנהל/ת",
            "תפקיד ברירת מחדל — נקודת התחלה לצפייה ואישור",
            [
                (PermissionKeys.FamiliesView, PermissionScopes.Organization),
                (PermissionKeys.AssistanceTypesView, PermissionScopes.Organization),
                (PermissionKeys.CommitteeDecisionsView, PermissionScopes.Organization),
                (PermissionKeys.CommitteeDecisionsApprove, PermissionScopes.Organization),
                (PermissionKeys.CommitteeDecisionsReject, PermissionScopes.Organization),
                (PermissionKeys.CommitteeDecisionsCancel, PermissionScopes.Organization),
                (PermissionKeys.AssistanceItemsView, PermissionScopes.Organization),
                (PermissionKeys.AssistanceItemsViewHistory, PermissionScopes.Organization),
                (PermissionKeys.SuppliersView, PermissionScopes.Organization),
            ]),
        new(
            FactoryPresetKeys.Finance,
            "כספים",
            "תפקיד ברירת מחדל — נקודת התחלה לניהול סוגי סיוע ותשלומים",
            [
                (PermissionKeys.AssistanceTypesView, PermissionScopes.Organization),
                (PermissionKeys.AssistanceTypesCreate, PermissionScopes.Organization),
                (PermissionKeys.AssistanceTypesEdit, PermissionScopes.Organization),
                (PermissionKeys.AssistanceTypesDeactivate, PermissionScopes.Organization),
                (PermissionKeys.AssistanceTypesRestore, PermissionScopes.Organization),
                (PermissionKeys.SuppliersView, PermissionScopes.Organization),
                (PermissionKeys.SuppliersEdit, PermissionScopes.Organization),
                (PermissionKeys.FamiliesView, PermissionScopes.Organization),
                (PermissionKeys.CommitteeDecisionsView, PermissionScopes.Organization),
                (PermissionKeys.AssistanceItemsView, PermissionScopes.Organization),
                (PermissionKeys.AssistanceItemsViewHistory, PermissionScopes.Organization),
                (PermissionKeys.PaymentsView, PermissionScopes.Organization),
                // Legacy proof-path execute (kept for coexistence; not export-batch create)
                (PermissionKeys.PaymentsExecute, PermissionScopes.Organization),
                (PermissionKeys.PaymentsUploadProof, PermissionScopes.Organization),
                (PermissionKeys.PaymentsMarkPaid, PermissionScopes.Organization),
                (PermissionKeys.PaymentsReturnToCoordinator, PermissionScopes.Organization),
                (PermissionKeys.PaymentsEnterReference, PermissionScopes.Organization),
                // Phase 16: create and download granted together but remain distinct keys
                (PermissionKeys.PaymentsExportBatchesCreate, PermissionScopes.Organization),
                (PermissionKeys.PaymentsExportBatchesDownload, PermissionScopes.Organization),
                (PermissionKeys.PaymentsExportBatchesCancel, PermissionScopes.Organization),
                (PermissionKeys.PaymentsExportBatchItemsCancel, PermissionScopes.Organization),
                (PermissionKeys.PaymentsEditAssistanceItems, PermissionScopes.Organization),
                (PermissionKeys.AssistanceItemsComplete, PermissionScopes.Organization),
            ]),
    ];

    public static IReadOnlyList<(string PermissionKey, string Scope)> GrantsForPreset(string factoryPresetKey) =>
        Presets.First(p => p.FactoryPresetKey == factoryPresetKey).Grants;
}
