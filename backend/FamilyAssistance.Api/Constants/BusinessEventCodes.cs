namespace FamilyAssistance.Api.Constants;

public static class BusinessEventCodes
{
    public const string OrganizationCreate = "AUD-001";
    public const string OrganizationSuspend = "AUD-002";
    public const string OrgAdminBootstrap = "AUD-003";
    public const string OrgUserCreate = "AUD-004";
    public const string OrgUserUpdate = "AUD-005";
    public const string OrgUserDisable = "AUD-006";
    public const string FamilyCreate = "AUD-007";
    public const string FamilyUpdate = "AUD-008";
    public const string FamilyDeactivate = "AUD-009";
    public const string AssistanceTypeCreate = "AUD-010";
    public const string AssistanceTypeUpdate = "AUD-011";
    public const string AssistanceTypeDeactivate = "AUD-012";
}
