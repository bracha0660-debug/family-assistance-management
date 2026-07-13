using System.Text;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Tests;

/// <summary>Phase 16 M95 — export sheet content contract (+ 11c Hebrew headers).</summary>
public sealed class ExportSheetContractTests
{
    [Fact]
    public void InternalFieldKeys_IncludeAllRequiredContractFields()
    {
        var keys = ExportSheetBuilder.InternalFieldKeys;
        Assert.Contains("decision_code", keys);
        Assert.Contains("family_code", keys);
        Assert.Contains("family_accounting_code", keys);
        Assert.Contains("family_name", keys);
        Assert.Contains("assistance_type", keys);
        Assert.Contains("assistance_type_code", keys); // קוד סוג סיוע
        Assert.Contains("original_approved_amount", keys);
        Assert.Contains("current_export_amount", keys);
        Assert.Contains("amount_adjustment_indicator", keys);
        Assert.Contains("amount_adjustment_reason", keys);
        Assert.Contains("amount_adjustment_explanation", keys);
        Assert.Contains("payment_target", keys);
        Assert.Contains("beneficiary", keys);
        Assert.Contains("supplier_accounting_code", keys);
        Assert.Contains("payment_method", keys);
        Assert.Contains("bank_number", keys);
        Assert.Contains("branch_number", keys);
        Assert.Contains("account_number", keys);
        Assert.Contains("account_holder_name", keys);
        Assert.Contains("batch_number", keys);
        Assert.Contains("export_date", keys);
        Assert.Contains("payment_reference", keys);
        Assert.Contains("assistance_item_id", keys);
        Assert.Contains("payment_execution_id", keys);
        Assert.Contains("export_batch_item_status", keys);
    }

    [Fact]
    public void Headers_AllFields_AreHebrew_PerArch11cFullLock()
    {
        var expected = new Dictionary<string, string>
        {
            ["batch_number"] = "מספר אצוות ייצוא",
            ["export_date"] = "תאריך ייצוא",
            ["decision_code"] = "קוד החלטה",
            ["family_code"] = "קוד משפחה",
            ["family_accounting_code"] = "קוד משפחה בהנהלת חשבונות",
            ["family_name"] = "שם משפחה",
            ["assistance_type"] = "סוג סיוע",
            ["assistance_type_code"] = "קוד סוג סיוע",
            ["original_approved_amount"] = "סכום שאושר במקור",
            ["current_export_amount"] = "סכום נוכחי לייצוא",
            ["amount_adjustment_indicator"] = "שינוי סכום",
            ["amount_adjustment_reason"] = "סיבת שינוי סכום",
            ["amount_adjustment_explanation"] = "הסבר שינוי סכום",
            ["payment_target"] = "יעד תשלום",
            ["beneficiary"] = "מוטב",
            ["supplier_name"] = "שם ספק",
            ["supplier_accounting_code"] = "קוד ספק בהנהלת חשבונות",
            ["payment_method"] = "אמצעי תשלום",
            ["bank_number"] = "מספר בנק",
            ["branch_number"] = "מספר סניף",
            ["account_number"] = "מספר חשבון",
            ["account_holder_name"] = "שם בעל החשבון",
            ["payment_reference"] = "אסמכתא",
            ["assistance_item_id"] = "מזהה פריט סיוע",
            ["payment_execution_id"] = "מזהה ביצוע תשלום",
            ["export_batch_item_status"] = "סטטוס שורת ייצוא",
        };

        Assert.Equal(expected.Count, ExportSheetBuilder.InternalFieldKeys.Length);
        Assert.Equal(expected.Count, ExportSheetBuilder.Headers.Length);
        Assert.Equal(expected.Count, ExportSheetBuilder.LockedHebrewHeaderKeys.Count);

        foreach (var key in ExportSheetBuilder.InternalFieldKeys)
        {
            Assert.True(expected.ContainsKey(key), $"Missing expected mapping for {key}");
            Assert.Equal(expected[key], ExportSheetBuilder.ToUserFacingHeader(key));
            Assert.Contains(expected[key], ExportSheetBuilder.Headers);
            // No English internal keys in user-facing headers.
            Assert.DoesNotContain(key, ExportSheetBuilder.Headers);
        }
    }

    [Fact]
    public void Headers_OrderMatchesInternalFieldKeys()
    {
        Assert.Equal(ExportSheetBuilder.InternalFieldKeys.Length, ExportSheetBuilder.Headers.Length);
        for (var i = 0; i < ExportSheetBuilder.InternalFieldKeys.Length; i++)
        {
            Assert.Equal(
                ExportSheetBuilder.ToUserFacingHeader(ExportSheetBuilder.InternalFieldKeys[i]),
                ExportSheetBuilder.Headers[i]);
        }
    }

    [Fact]
    public void BuildCsv_HeaderLine_UsesFullHebrewLockSet()
    {
        var batch = SampleBatch("EB-000100");
        var item = SampleItem(batch.Id, original: 500m, exported: 500m, reason: null, explanation: null);
        var csv = Encoding.UTF8.GetString(ExportSheetBuilder.BuildCsv(batch, [item]));
        var headerLine = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]
            .TrimStart('\uFEFF');
        Assert.Contains("קוד סוג סיוע", headerLine);
        Assert.Contains("קוד משפחה בהנהלת חשבונות", headerLine);
        Assert.Contains("סכום נוכחי לייצוא", headerLine);
        Assert.Contains("סיבת שינוי סכום", headerLine);
        Assert.Contains("קוד ספק בהנהלת חשבונות", headerLine);
        Assert.Contains("סטטוס שורת ייצוא", headerLine);
        Assert.DoesNotContain("assistance_type_code", headerLine);
        Assert.DoesNotContain("export_batch_item_status", headerLine);
    }

    [Fact]
    public void BuildCsv_UsesCurrentExportAmount_NotSilentlyOriginal()
    {
        var batch = SampleBatch("EB-000101");
        var item = SampleItem(batch.Id, original: 500m, exported: 503m, reason: AmountAdjustmentReasons.TypingError, explanation: "should-not-appear");
        var csv = Encoding.UTF8.GetString(ExportSheetBuilder.BuildCsv(batch, [item]));
        var dataLine = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[1];
        Assert.Contains("500", dataLine);
        Assert.Contains("503", dataLine);
        Assert.Contains("yes", dataLine);
        // non-other reason → explanation column empty (not the stored stray text)
        Assert.DoesNotContain("should-not-appear", dataLine);
    }

    [Fact]
    public void BuildCsv_OtherReason_IncludesExplanation()
    {
        var batch = SampleBatch("EB-000102");
        var item = SampleItem(batch.Id, original: 500m, exported: 480m, reason: AmountAdjustmentReasons.Other, explanation: "הנחה מיוחדת");
        var csv = Encoding.UTF8.GetString(ExportSheetBuilder.BuildCsv(batch, [item]));
        Assert.Contains("הנחה מיוחדת", csv);
        Assert.Contains("other", csv);
    }

    [Fact]
    public void BuildCsv_IncludesAssistanceTypeCode_AndBankDetails()
    {
        var batch = SampleBatch("EB-000103");
        var item = SampleItem(batch.Id, original: 500m, exported: 500m, reason: null, explanation: null);
        item.AssistanceTypeCode = "5030";
        item.TransferBankNumber = "12";
        item.TransferBranchNumber = "345";
        item.TransferAccountNumber = "123456";
        item.AccountHolderName = "כהן";
        var csv = Encoding.UTF8.GetString(ExportSheetBuilder.BuildCsv(batch, [item]));
        Assert.Contains("5030", csv);
        Assert.Contains("12", csv);
        Assert.Contains("345", csv);
        Assert.Contains("123456", csv);
        Assert.Contains("כהן", csv);
        Assert.Contains("no", csv); // not adjusted
    }

    [Fact]
    public void BuildCsv_SameBatchNumberOnRegenerate_FromSnapshot()
    {
        var batch = SampleBatch("EB-000104");
        var item = SampleItem(batch.Id, original: 500m, exported: 500m, reason: null, explanation: null);
        var first = Encoding.UTF8.GetString(ExportSheetBuilder.BuildCsv(batch, [item]));
        var second = Encoding.UTF8.GetString(ExportSheetBuilder.BuildCsv(batch, [item]));
        Assert.Equal(first, second);
        Assert.Contains("EB-000104", first);
    }

    [Fact]
    public void ResolveBankDetails_FamilyBankTransfer_UsesFamilyFields()
    {
        var familyId = Guid.NewGuid();
        var item = new AssistanceItem
        {
            PaymentMethod = PaymentMethods.BankTransfer,
            PaymentTarget = PaymentTargets.Family,
            TransferBankNumber = "99",
            CommitteeDecision = new CommitteeDecision
            {
                Family = new Family
                {
                    Id = familyId,
                    BankNumber = "10",
                    BranchNumber = "20",
                    AccountNumber = "30",
                    AccountHolderName = "משפחה"
                }
            }
        };

        var (bank, branch, account, holder) = ExportSheetBuilder.ResolveBankDetails(item);
        Assert.Equal("10", bank);
        Assert.Equal("20", branch);
        Assert.Equal("30", account);
        Assert.Equal("משפחה", holder);
    }

    private static ExportBatch SampleBatch(string number) => new()
    {
        Id = Guid.NewGuid(),
        BatchNumber = number,
        Status = ExportBatchStatuses.Open,
        CreatedAt = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
        GeneratedAt = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc)
    };

    private static ExportBatchItem SampleItem(
        Guid batchId, decimal original, decimal exported, string? reason, string? explanation) => new()
    {
        Id = Guid.NewGuid(),
        ExportBatchId = batchId,
        AssistanceItemId = Guid.NewGuid(),
        PaymentExecutionId = Guid.NewGuid(),
        DecisionCode = "D-000001",
        FamilyCode = "F-1",
        FamilyAccountingCode = 1001,
        FamilyName = "כהן",
        AssistanceTypeName = "לימודים",
        AssistanceTypeCode = "5030",
        OriginalApprovedAmount = original,
        ExportedAmount = exported,
        AmountAdjustmentReason = reason,
        AmountAdjustmentExplanation = explanation,
        PaymentTarget = PaymentTargets.Family,
        PaymentMethod = PaymentMethods.BankTransfer,
        Status = ExportBatchItemStatuses.Active
    };
}
