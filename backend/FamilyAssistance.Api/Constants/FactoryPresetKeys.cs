namespace FamilyAssistance.Api.Constants;

public static class FactoryPresetKeys
{
    public const string Coordinator = "preset_coordinator";
    public const string Manager = "preset_manager";
    public const string Finance = "preset_finance";

    public static readonly string[] All = [Coordinator, Manager, Finance];

    /// <summary>Legacy role string → factory preset key (migration mapping only).</summary>
    public static string? FromLegacyRole(string role) => role switch
    {
        Roles.Coordinator => Coordinator,
        Roles.Manager => Manager,
        Roles.Finance => Finance,
        _ => null
    };
}
