namespace FamilyAssistance.Api.Validation;

public static class IsraeliIdNormalizer
{
    public static string? Normalize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var digits = new string(id.Trim().Where(char.IsDigit).ToArray());
        if (digits.Length == 0 || digits.Length > 9)
            return null;

        return digits.PadLeft(9, '0');
    }

    public static bool TryNormalize(string? id, out string? normalized)
    {
        normalized = Normalize(id);
        return normalized is not null;
    }
}
