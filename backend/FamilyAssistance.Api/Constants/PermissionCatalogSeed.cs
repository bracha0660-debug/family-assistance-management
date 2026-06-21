using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Constants;

public static class PermissionCatalogSeed
{
    public static readonly PermissionCatalog[] Rows =
    [
        Row(PermissionKeys.FamiliesView, "families", "צפייה במשפחות", "צפייה ברשימת משפחות ובפרטי משפחה", 10, true, true),
        Row(PermissionKeys.FamiliesCreate, "families", "יצירת משפחות", "הוספת משפחה חדשה לארגון", 20, false, false),
        Row(PermissionKeys.FamiliesEdit, "families", "עריכת משפחות", "עדכון פרטי משפחה", 30, true, true),
        Row(PermissionKeys.FamiliesDeactivate, "families", "השבתת משפחות", "השבתת משפחה (לא מחיקה)", 40, true, true),
        Row(PermissionKeys.FamiliesRestore, "families", "שחזור משפחות", "שחזור משפחה שהושבתה", 50, true, true),
        Row(PermissionKeys.FamiliesExport, "families", "ייצוא משפחות", "ייצוא נתוני משפחות", 60, true, true),
        Row(PermissionKeys.SuppliersView, "suppliers", "צפייה בספקים", "צפייה ברשימת ספקים ובפרטי ספק", 10, false, true),
        Row(PermissionKeys.SuppliersCreate, "suppliers", "יצירת ספקים", "הוספת ספק חדש", 20, false, false),
        Row(PermissionKeys.SuppliersEdit, "suppliers", "עריכת ספקים", "עדכון פרטי ספק", 30, false, true),
        Row(PermissionKeys.SuppliersDeactivate, "suppliers", "השבתת ספקים", "השבתת ספק", 40, false, true),
        Row(PermissionKeys.SuppliersRestore, "suppliers", "שחזור ספקים", "שחזור ספק שהושבת", 50, false, true),
        Row(PermissionKeys.SuppliersExport, "suppliers", "ייצוא ספקים", "ייצוא נתוני ספקים", 60, false, true),
        Row(PermissionKeys.AssistanceTypesView, "assistance_types", "צפייה בסוגי סיוע", "צפייה ברשימת סוגי סיוע", 10, false, true),
        Row(PermissionKeys.AssistanceTypesCreate, "assistance_types", "יצירת סוגי סיוע", "הוספת סוג סיוע חדש", 20, false, false),
        Row(PermissionKeys.AssistanceTypesEdit, "assistance_types", "עריכת סוגי סיוע", "עדכון סוג סיוע", 30, false, true),
        Row(PermissionKeys.AssistanceTypesDeactivate, "assistance_types", "השבתת סוגי סיוע", "השבתת סוג סיוע", 40, false, true),
        Row(PermissionKeys.AssistanceTypesRestore, "assistance_types", "שחזור סוגי סיוע", "שחזור סוג סיוע שהושבת", 50, false, true),
        Row(PermissionKeys.CommitteeDecisionsView, "committee_decisions", "צפייה בהחלטות ועדה", "צפייה בהחלטות ועדה", 10, true, true),
        Row(PermissionKeys.CommitteeDecisionsCreate, "committee_decisions", "יצירת החלטת ועדה", "פתיחת החלטת ועדה חדשה", 20, false, false),
        Row(PermissionKeys.CommitteeDecisionsEditDraft, "committee_decisions", "עריכת טיוטת החלטה", "עריכת החלטה במצב טיוטה", 30, true, true),
        Row(PermissionKeys.CommitteeDecisionsSubmit, "committee_decisions", "הגשת החלטה לועדה", "שליחת טיוטה לאישור", 40, true, true),
        Row(PermissionKeys.CommitteeDecisionsApprove, "committee_decisions", "אישור החלטת ועדה", "אישור החלטה — organization scope only", 50, true, true),
        Row(PermissionKeys.CommitteeDecisionsReject, "committee_decisions", "דחיית החלטת ועדה", "דחיית החלטה — organization scope only", 60, true, true),
        Row(PermissionKeys.CommitteeDecisionsCancel, "committee_decisions", "ביטול החלטת ועדה", "ביטול החלטת ועדה", 70, true, true),
        Row(PermissionKeys.AssistanceItemsView, "assistance_items", "צפייה בפריטי סיוע", "צפייה בפריטי סיוע בהחלטה", 10, true, true),
        Row(PermissionKeys.AssistanceItemsCreate, "assistance_items", "הוספת פריט סיוע", "הוספת פריט סיוע לטיוטה", 20, false, false),
        Row(PermissionKeys.AssistanceItemsEdit, "assistance_items", "עריכת פריט סיוע", "עריכת פריט סיוע בטיוטה", 30, true, true),
        Row(PermissionKeys.AssistanceItemsRemoveDraft, "assistance_items", "הסרת פריט מטיוטה", "הסרת פריט סיוע מטיוטה", 40, true, true),
        Row(PermissionKeys.PaymentsView, "payments", "צפייה בתשלומים", "צפייה בתור תשלומים, בסטטוס ביצוע ובפרטי תשלום", 10, false, true),
        Row(PermissionKeys.PaymentsExecute, "payments", "ביצוע תשלום", "ייזום/רישום ביצוע תשלום לפריט מאושר", 20, false, true),
        Row(PermissionKeys.PaymentsUploadProof, "payments", "העלאת אישור ביצוע", "העלאת מסמך אישור ביצוע (קבלה, אישור בנק וכו')", 30, false, true),
        Row(PermissionKeys.PaymentsMarkPaid, "payments", "סימון כשולם", "סימון תשלום כשולם — מעבר לסטטוס סופי", 40, false, true),
        Row(PermissionKeys.PaymentsReturnToCoordinator, "payments", "החזרה לרכז", "החזרת פריט תשלום לרכז לתיקון/השלמה", 50, false, true),
    ];

    private static PermissionCatalog Row(
        string key, string category, string nameHe, string? descHe, int sort,
        bool supportsMyRecords, bool scopeApplies) => new()
    {
        PermissionKey = key,
        Category = category,
        DisplayNameHe = nameHe,
        DescriptionHe = descHe,
        SortOrder = sort,
        IsActive = true,
        SupportsMyRecords = supportsMyRecords,
        ScopeApplies = scopeApplies,
    };
}
