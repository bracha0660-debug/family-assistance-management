namespace FamilyAssistance.Api.Validation;

public static class BankFieldValidator
{
    public const string BankRequiredMessage = "פרטי בנק הם שדות חובה";
    public const string DigitsOnlyMessage = "שדות בנק חייבים להכיל ספרות בלבד";
    public const string HolderRequiredMessage = "שם בעל החשבון הוא שדה חובה";

    public static List<string> ValidateCreate(string? bankNumber, string? branchNumber, string? accountNumber, string? accountHolderName)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(bankNumber) ||
            string.IsNullOrWhiteSpace(branchNumber) ||
            string.IsNullOrWhiteSpace(accountNumber) ||
            string.IsNullOrWhiteSpace(accountHolderName))
        {
            errors.Add(BankRequiredMessage);
            return errors;
        }

        ValidateDigits(bankNumber.Trim(), "bank_number", errors);
        ValidateDigits(branchNumber.Trim(), "branch_number", errors);
        ValidateDigits(accountNumber.Trim(), "account_number", errors);

        if (accountHolderName.Trim().Length == 0)
            errors.Add(HolderRequiredMessage);

        return errors;
    }

    public static bool IsDigitsOnly(string value) =>
        value.Length > 0 && value.All(char.IsDigit);

    private static void ValidateDigits(string value, string _, List<string> errors)
    {
        if (!IsDigitsOnly(value))
            errors.Add(DigitsOnlyMessage);
    }
}
