namespace FamilyAssistance.Api.Constants;

public static class PermissionOverrideEffects
{
    public const string Grant = "grant";
    public const string Deny = "deny";

    public static readonly string[] All = [Grant, Deny];
}
