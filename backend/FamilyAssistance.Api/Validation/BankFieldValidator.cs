using FamilyAssistance.Api.Data;

namespace FamilyAssistance.Api.Validation;

public static class BankFieldValidator
{
    public const string PartialBankMessage = "פרטי בנק חייבים להיות מלאים או ריקים";
    public const string DigitsOnlyMessage = "שדות בנק חייבים להכיל ספרות בלבד";
    public const string HolderRequiredMessage = "שם בעל החשבון הוא שדה חובה";
    public const string UnknownBankNumberMessage = "מספר בנק אינו מזוהה";
    public const string IncompleteBankForPaymentMessage = "פרטי בנק אינם שלמים — לא ניתן לבצע העברה בנקאית";

    public static bool IsAllEmpty(string? bankNumber, string? branchNumber, string? accountNumber, string? accountHolderName)
    {
        return string.IsNullOrWhiteSpace(bankNumber)
            && string.IsNullOrWhiteSpace(branchNumber)
            && string.IsNullOrWhiteSpace(accountNumber)
            && string.IsNullOrWhiteSpace(accountHolderName);
    }

    /// <summary>Card save: all empty, or all four fields complete and valid. Partial state is rejected.</summary>
    public static List<string> ValidateForSave(string? bankNumber, string? branchNumber, string? accountNumber, string? accountHolderName)
    {
        var bank = NormalizeOptional(bankNumber);
        var branch = NormalizeOptional(branchNumber);
        var account = NormalizeOptional(accountNumber);
        var holder = NormalizeOptional(accountHolderName);

        if (IsAllEmpty(bank, branch, account, holder))
            return [];

        if (bank is null || branch is null || account is null || holder is null)
            return [PartialBankMessage];

        return ValidateCompleteValues(bank, branch, account, holder);
    }

    /// <summary>Payment execute: bank transfer to family/supplier requires complete bank details.</summary>
    public static List<string> ValidateCompleteForPayment(string? bankNumber, string? branchNumber, string? accountNumber, string? accountHolderName)
    {
        var bank = NormalizeOptional(bankNumber);
        var branch = NormalizeOptional(branchNumber);
        var account = NormalizeOptional(accountNumber);
        var holder = NormalizeOptional(accountHolderName);

        if (IsAllEmpty(bank, branch, account, holder))
            return [IncompleteBankForPaymentMessage];

        if (bank is null || branch is null || account is null || holder is null)
            return [IncompleteBankForPaymentMessage];

        var errors = ValidateCompleteValues(bank, branch, account, holder);
        return errors.Count > 0 ? [IncompleteBankForPaymentMessage] : errors;
    }

    public static bool IsDigitsOnly(string value) =>
        value.Length > 0 && value.All(char.IsDigit);

    private static List<string> ValidateCompleteValues(string bank, string branch, string account, string holder)
    {
        var errors = new List<string>();

        if (!IsDigitsOnly(bank))
            errors.Add(DigitsOnlyMessage);
        else if (!IsraeliBanks.IsKnownBankNumber(bank))
            errors.Add(UnknownBankNumberMessage);

        if (!IsDigitsOnly(branch))
            errors.Add(DigitsOnlyMessage);

        if (!IsDigitsOnly(account))
            errors.Add(DigitsOnlyMessage);

        if (holder.Length == 0)
            errors.Add(HolderRequiredMessage);

        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
