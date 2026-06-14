namespace FamilyAssistance.Api.Validation;

public static class IsraeliIdValidator
{
    public static bool IsValid(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length != 9)
            return false;

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var ch = id[i];
            if (ch < '0' || ch > '9')
                return false;

            var digit = ch - '0';
            var weight = (i % 2) + 1;
            var product = digit * weight;
            if (product > 9)
                product -= 9;

            sum += product;
        }

        return sum % 10 == 0;
    }
}
