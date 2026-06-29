using System.Net.Mail;

namespace FamilyAssistance.Api.Validation;

public static class EmailValidator
{
    public const string InvalidMessage = "כתובת אימייל לא תקינה";
    public const string MaxLengthMessage = "כתובת אימייל חייבת להיות עד 254 תווים";

    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return null;

        if (trimmed.Length > 254)
            return MaxLengthMessage;

        try
        {
            var address = new MailAddress(trimmed);
            if (!string.Equals(address.Address, trimmed, StringComparison.Ordinal))
                return InvalidMessage;
        }
        catch (FormatException)
        {
            return InvalidMessage;
        }

        return null;
    }
}
