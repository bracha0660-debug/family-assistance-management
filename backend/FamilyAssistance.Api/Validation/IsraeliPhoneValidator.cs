using System.Text.RegularExpressions;

namespace FamilyAssistance.Api.Validation;

public static class IsraeliPhoneValidator
{
    public const string PrefixRequiredMessage = "קידומת היא שדה חובה";
    public const string PrefixDigitsMessage = "קידומת חייבת להכיל ספרות בלבד";
    public const string PrefixLengthMessage = "קידומת חייבת להכיל 2 או 3 ספרות";
    public const string NumberRequiredMessage = "מספר טלפון הוא שדה חובה";
    public const string NumberDigitsMessage = "מספר טלפון חייב להכיל ספרות בלבד";
    public const string NumberLengthMessage = "מספר טלפון חייב להכיל 7 ספרות";
    public const string MaxLengthMessage = "טלפון חייב להיות עד 30 תווים";

    private static readonly Regex DashedPhoneRegex = new(@"^(\d+)-(\d+)$", RegexOptions.CultureInvariant);

    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return null;

        if (trimmed.Length > 30)
            return MaxLengthMessage;

        var (prefix, number) = ParseParts(trimmed);
        return ValidateParts(prefix, number);
    }

    private static (string Prefix, string Number) ParseParts(string trimmed)
    {
        var dashed = DashedPhoneRegex.Match(trimmed);
        if (dashed.Success)
            return (dashed.Groups[1].Value, dashed.Groups[2].Value);

        if (!trimmed.All(char.IsDigit))
            return (trimmed, string.Empty);

        if (trimmed.Length <= 3)
            return (trimmed, string.Empty);

        // joinPhoneValue sends number-only as 7 digits without prefix
        if (trimmed.Length == 7)
            return (string.Empty, trimmed);

        if (trimmed.Length == 9)
            return (trimmed[..2], trimmed[2..]);

        if (trimmed.Length == 10)
            return (trimmed[..3], trimmed[3..]);

        return (trimmed[..3], trimmed[3..]);
    }

    private static string? ValidateParts(string prefix, string number)
    {
        if (prefix.Length == 0 && number.Length == 0)
            return null;

        if (prefix.Length == 0)
            return PrefixRequiredMessage;

        if (!prefix.All(char.IsDigit))
            return PrefixDigitsMessage;

        if (prefix.Length is not 2 and not 3)
            return PrefixLengthMessage;

        if (number.Length == 0)
            return NumberRequiredMessage;

        if (!number.All(char.IsDigit))
            return NumberDigitsMessage;

        if (number.Length != 7)
            return NumberLengthMessage;

        return null;
    }
}
