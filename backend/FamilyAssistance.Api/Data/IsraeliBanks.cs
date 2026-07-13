namespace FamilyAssistance.Api.Data;

public sealed record IsraeliBank(string Number, string Name);

public static class IsraeliBanks
{
    public static readonly IReadOnlyList<IsraeliBank> All =
    [
        new("4", "בנק יהב לעובדי המדינה בע\"מ"),
        new("9", "בנק הדואר"),
        new("10", "בנק לאומי לישראל בע\"מ"),
        new("11", "בנק דיסקונט לישראל בע\"מ"),
        new("12", "בנק הפועלים בע\"מ"),
        new("13", "בנק אגוד לישראל בע\"מ"),
        new("14", "בנק אוצר החייל בע\"מ"),
        new("17", "בנק מרכנתיל דיסקונט בע\"מ"),
        new("20", "בנק מזרחי טפחות בע\"מ"),
        new("31", "בנק הבינלאומי הראשון לישראל בע\"מ"),
        new("46", "בנק מסד בע\"מ"),
        new("52", "בנק פועלי אגודת ישראל בע\"מ"),
        new("54", "בנק ירושלים בע\"מ"),
    ];

    public static string? NormalizeBankNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        return int.Parse(digits, System.Globalization.NumberStyles.None).ToString();
    }

    public static IsraeliBank? FindByNumber(string? bankNumber)
    {
        var normalized = NormalizeBankNumber(bankNumber);
        if (normalized is null) return null;
        return All.FirstOrDefault(b => b.Number == normalized);
    }

    public static bool IsKnownBankNumber(string? bankNumber) => FindByNumber(bankNumber) is not null;
}
