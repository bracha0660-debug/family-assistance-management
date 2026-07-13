using System.Globalization;
using System.Text;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Services;

/// <summary>
/// Phase 16 M95 — export sheet content contract.
/// File content is independent of Payments table visible columns.
/// Uses current exported amount (never silently the original if adjusted).
/// User-facing CSV headers are all Hebrew per Architect 11c full LOCKED table (2026-07-12).
/// </summary>
public static class ExportSheetBuilder
{
    /// <summary>Stable internal field keys (API/DB/code identity — unchanged).</summary>
    public static readonly string[] InternalFieldKeys =
    [
        "batch_number",
        "export_date",
        "decision_code",
        "family_code",
        "family_accounting_code",
        "family_name",
        "assistance_type",
        "assistance_type_code", // קוד סוג סיוע
        "original_approved_amount",
        "current_export_amount",
        "amount_adjustment_indicator",
        "amount_adjustment_reason",
        "amount_adjustment_explanation",
        "payment_target",
        "beneficiary",
        "supplier_name",
        "supplier_accounting_code",
        "payment_method",
        "bank_number",
        "branch_number",
        "account_number",
        "account_holder_name",
        "payment_reference",
        "assistance_item_id",
        "payment_execution_id",
        "export_batch_item_status"
    ];

    /// <summary>
    /// User-facing CSV header row. All keys → Hebrew per arch 11c full LOCKED table (2026-07-12).
    /// </summary>
    public static readonly string[] Headers =
        InternalFieldKeys.Select(ToUserFacingHeader).ToArray();

    /// <summary>Full Architect-approved Hebrew header set (11c, locked 2026-07-12).</summary>
    public static readonly HashSet<string> LockedHebrewHeaderKeys =
        InternalFieldKeys.ToHashSet(StringComparer.Ordinal);

    public static string ToUserFacingHeader(string internalKey) => internalKey switch
    {
        "batch_number" => "מספר אצוות ייצוא",
        "export_date" => "תאריך ייצוא",
        "decision_code" => "קוד החלטה",
        "family_code" => "קוד משפחה",
        "family_accounting_code" => "קוד משפחה בהנהלת חשבונות",
        "family_name" => "שם משפחה",
        "assistance_type" => "סוג סיוע",
        "assistance_type_code" => "קוד סוג סיוע",
        "original_approved_amount" => "סכום שאושר במקור",
        "current_export_amount" => "סכום נוכחי לייצוא",
        "amount_adjustment_indicator" => "שינוי סכום",
        "amount_adjustment_reason" => "סיבת שינוי סכום",
        "amount_adjustment_explanation" => "הסבר שינוי סכום",
        "payment_target" => "יעד תשלום",
        "beneficiary" => "מוטב",
        "supplier_name" => "שם ספק",
        "supplier_accounting_code" => "קוד ספק בהנהלת חשבונות",
        "payment_method" => "אמצעי תשלום",
        "bank_number" => "מספר בנק",
        "branch_number" => "מספר סניף",
        "account_number" => "מספר חשבון",
        "account_holder_name" => "שם בעל החשבון",
        "payment_reference" => "אסמכתא",
        "assistance_item_id" => "מזהה פריט סיוע",
        "payment_execution_id" => "מזהה ביצוע תשלום",
        "export_batch_item_status" => "סטטוס שורת ייצוא",
        _ => throw new ArgumentOutOfRangeException(nameof(internalKey), internalKey, "Unknown export field key")
    };

    public static byte[] BuildCsv(ExportBatch batch, IReadOnlyList<ExportBatchItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Headers));

        var exportDate = (batch.GeneratedAt ?? batch.CreatedAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var i in items)
        {
            var adjusted = i.OriginalApprovedAmount != i.ExportedAmount;
            var explanation = AmountAdjustmentReasons.RequiresExplanation(i.AmountAdjustmentReason)
                ? i.AmountAdjustmentExplanation
                : null;

            sb.AppendLine(string.Join(",",
            [
                Csv(batch.BatchNumber),
                Csv(exportDate),
                Csv(i.DecisionCode),
                Csv(i.FamilyCode),
                Csv(i.FamilyAccountingCode?.ToString(CultureInfo.InvariantCulture)),
                Csv(i.FamilyName),
                Csv(i.AssistanceTypeName),
                Csv(i.AssistanceTypeCode),
                Csv(i.OriginalApprovedAmount.ToString(CultureInfo.InvariantCulture)),
                Csv(i.ExportedAmount.ToString(CultureInfo.InvariantCulture)),
                Csv(adjusted ? "yes" : "no"),
                Csv(i.AmountAdjustmentReason),
                Csv(explanation),
                Csv(i.PaymentTarget),
                Csv(ResolveBeneficiary(i)),
                Csv(i.SupplierName),
                Csv(i.SupplierAccountingCode),
                Csv(i.PaymentMethod),
                Csv(i.TransferBankNumber),
                Csv(i.TransferBranchNumber),
                Csv(i.TransferAccountNumber),
                Csv(i.AccountHolderName),
                Csv(i.ExecutionReference),
                Csv(i.AssistanceItemId.ToString("D")),
                Csv(i.PaymentExecutionId.ToString("D")),
                Csv(i.Status)
            ]));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>Resolve bank/payment details from family, supplier, or item transfer fields at export time.</summary>
    public static (string? Bank, string? Branch, string? Account, string? Holder) ResolveBankDetails(AssistanceItem item)
    {
        var family = item.CommitteeDecision?.Family;
        var supplier = item.Supplier;

        if (item.PaymentMethod == PaymentMethods.BankTransfer)
        {
            if (item.PaymentTarget == PaymentTargets.Family && family is not null)
            {
                return (family.BankNumber, family.BranchNumber, family.AccountNumber, family.AccountHolderName);
            }

            if (item.PaymentTarget == PaymentTargets.Supplier && supplier is not null)
            {
                return (supplier.BankNumber, supplier.BranchNumber, supplier.AccountNumber, supplier.AccountHolderName);
            }
        }

        return (
            item.TransferBankNumber,
            item.TransferBranchNumber,
            item.TransferAccountNumber,
            item.AccountHolderName ?? item.PayeeName);
    }

    private static string ResolveBeneficiary(ExportBatchItem i)
    {
        if (!string.IsNullOrWhiteSpace(i.PayeeName))
            return i.PayeeName;
        if (i.PaymentTarget == PaymentTargets.Supplier && !string.IsNullOrWhiteSpace(i.SupplierName))
            return i.SupplierName!;
        if (i.PaymentTarget == PaymentTargets.Family)
            return i.FamilyName;
        return i.AccountHolderName ?? string.Empty;
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
