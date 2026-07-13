namespace FamilyAssistance.Api.Validation;

public static class IsraeliCompanyRegistrationValidator
{
    public const string RequiredMessage = "שדה חובה";
    public const string MinDigitsMessage = "מספר עוסק / ח.פ. חייב להכיל לפחות 9 ספרות";
    public const string DigitsOnlyMessage = "מספר עוסק / ח.פ. חייב להכיל ספרות בלבד";
    public const string InvalidMessage = "מספר עוסק / ח.פ. אינו תקין";

    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return RequiredMessage;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return RequiredMessage;

        if (!trimmed.All(char.IsDigit))
            return DigitsOnlyMessage;

        if (trimmed.Length < 9)
            return MinDigitsMessage;

        if (trimmed.Length > 9)
            return InvalidMessage;

        return IsraeliIdValidator.IsValid(trimmed) ? null : InvalidMessage;
    }

    public static string? NormalizeValid(string? value)
    {
        var error = Validate(value);
        if (error is not null)
            return null;

        return value!.Trim();
    }
}
