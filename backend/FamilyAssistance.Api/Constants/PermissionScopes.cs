namespace FamilyAssistance.Api.Constants;

public static class PermissionScopes
{
    public const string MyRecords = "my_records";
    public const string Organization = "organization";

    public static readonly string[] All = [MyRecords, Organization];
}
