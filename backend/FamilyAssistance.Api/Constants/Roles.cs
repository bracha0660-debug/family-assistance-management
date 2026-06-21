namespace FamilyAssistance.Api.Constants;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string OrganizationAdministrator = "OrganizationAdministrator";
    public const string Coordinator = "Coordinator";
    public const string Manager = "Manager";
    public const string Finance = "Finance";
    public const string OrganizationUser = "OrganizationUser";

    public static readonly string[] All =
    [
        SuperAdmin,
        OrganizationAdministrator,
        Coordinator,
        Manager,
        Finance,
        OrganizationUser
    ];
}
