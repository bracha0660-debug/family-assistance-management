using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Validation;

public static class CommitteeItemPaymentRules
{
    public const string FamilyBankIncompleteMessage = "יש לעדכן פרטי חשבון בנק בכרטיס המשפחה";
    public const string SupplierBankIncompleteMessage = "יש לעדכן פרטי חשבון בנק במסך הספקים";
    public const string SupplierVouchersMessage = "ספק אינו יכול לקבל תשלום בתווים";
    public const string PayeeNameRequiredMessage = "יש לציין שם מוטב";
    public const string TransferBankRequiredMessage = "יש להזין פרטי העברה בנקאית";

    private static readonly Dictionary<string, HashSet<string>> AllowedMethods = new()
    {
        [PaymentTargets.Family] = [PaymentMethods.BankTransfer, PaymentMethods.Check, PaymentMethods.Vouchers],
        [PaymentTargets.Supplier] = [PaymentMethods.BankTransfer, PaymentMethods.Check],
        [PaymentTargets.Other] = [PaymentMethods.BankTransfer, PaymentMethods.Check, PaymentMethods.Vouchers],
    };

    public static bool IsMethodAllowedForTarget(string paymentTarget, string paymentMethod) =>
        AllowedMethods.TryGetValue(paymentTarget, out var methods) && methods.Contains(paymentMethod);

    public static List<string> ValidateItemFields(
        string paymentTarget,
        string paymentMethod,
        Guid? supplierId,
        string? payeeName,
        string? voucherType)
    {
        var errors = new List<string>();

        if (!PaymentTargets.All.Contains(paymentTarget))
            errors.Add("יעד תשלום לא חוקי");
        if (!PaymentMethods.All.Contains(paymentMethod))
            errors.Add("אמצעי תשלום לא חוקי");

        if (paymentTarget == PaymentTargets.Supplier && supplierId is null)
            errors.Add("יש לבחור ספק");

        if ((paymentTarget == PaymentTargets.Family || paymentTarget == PaymentTargets.Other)
            && string.IsNullOrWhiteSpace(payeeName))
            errors.Add(PayeeNameRequiredMessage);

        if (paymentMethod == PaymentMethods.Vouchers && string.IsNullOrWhiteSpace(voucherType))
            errors.Add("יש לציין סוג שובר");

        if (!IsMethodAllowedForTarget(paymentTarget, paymentMethod))
        {
            if (paymentTarget == PaymentTargets.Supplier && paymentMethod == PaymentMethods.Vouchers)
                errors.Add(SupplierVouchersMessage);
            else
                errors.Add("אופן תשלום לא חוקי ליעד שנבחר");
        }

        return errors;
    }

    public static List<string> ValidateBankForTransfer(
        string paymentTarget,
        string paymentMethod,
        Family family,
        Supplier? supplier)
    {
        if (paymentMethod != PaymentMethods.BankTransfer)
            return [];

        if (paymentTarget == PaymentTargets.Family)
        {
            if (!IsBankCompleteForPayment(
                    family.BankNumber, family.BranchNumber, family.AccountNumber, family.AccountHolderName))
                return [FamilyBankIncompleteMessage];
        }
        else if (paymentTarget == PaymentTargets.Supplier)
        {
            if (supplier is null || !IsBankCompleteForPayment(
                    supplier.BankNumber, supplier.BranchNumber, supplier.AccountNumber, supplier.AccountHolderName))
                return [SupplierBankIncompleteMessage];
        }

        return [];
    }

    public static List<string> ValidateTransferBankForOther(
        string paymentTarget,
        string paymentMethod,
        string? payeeName,
        string? transferBankNumber,
        string? transferBranchNumber,
        string? transferAccountNumber)
    {
        if (paymentTarget != PaymentTargets.Other || paymentMethod != PaymentMethods.BankTransfer)
            return [];

        var holder = payeeName?.Trim() ?? string.Empty;
        var errors = BankFieldValidator.ValidateCompleteForPayment(
            transferBankNumber, transferBranchNumber, transferAccountNumber, holder);
        return errors.Count > 0 ? [TransferBankRequiredMessage] : [];
    }

    public static void SyncTransferBankStorage(AssistanceItem item)
    {
        if (item.PaymentTarget == PaymentTargets.Other && item.PaymentMethod == PaymentMethods.BankTransfer)
            return;

        item.TransferBankNumber = null;
        item.TransferBranchNumber = null;
        item.TransferAccountNumber = null;
    }

    public static void ApplyTransferBankFromRequest(
        AssistanceItem item,
        string? transferBankNumber,
        string? transferBranchNumber,
        string? transferAccountNumber,
        bool clearTransferBank)
    {
        if (clearTransferBank)
        {
            item.TransferBankNumber = null;
            item.TransferBranchNumber = null;
            item.TransferAccountNumber = null;
            return;
        }

        if (item.PaymentTarget != PaymentTargets.Other || item.PaymentMethod != PaymentMethods.BankTransfer)
        {
            SyncTransferBankStorage(item);
            return;
        }

        if (transferBankNumber is not null)
            item.TransferBankNumber = NormalizeOptional(transferBankNumber);
        if (transferBranchNumber is not null)
            item.TransferBranchNumber = NormalizeOptional(transferBranchNumber);
        if (transferAccountNumber is not null)
            item.TransferAccountNumber = NormalizeOptional(transferAccountNumber);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool IsBankCompleteForPayment(
        string? bankNumber,
        string? branchNumber,
        string? accountNumber,
        string? accountHolderName)
    {
        if (BankFieldValidator.IsAllEmpty(bankNumber, branchNumber, accountNumber, accountHolderName))
            return false;

        return BankFieldValidator.ValidateCompleteForPayment(
            bankNumber, branchNumber, accountNumber, accountHolderName).Count == 0;
    }
}
