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
    public const string SupplierCreate = "AUD-013";
    public const string SupplierUpdate = "AUD-014";
    public const string SupplierIdentityChange = "AUD-015";
    public const string RolePermissionsUpdate = "AUD-016";
    public const string FamilyBankChange = "AUD-027";
    public const string FamilyRestore = "AUD-028";
    public const string PaymentExecutionStarted = "AUD-029";
    public const string PaymentProofUploaded = "AUD-030";
    public const string PaymentMarkedPaid = "AUD-031";
    public const string PaymentReturnedToCoordinator = "AUD-032";
    public const string CommitteeDecisionCreate = "AUD-033";
    public const string CommitteeDecisionStatusChange = "AUD-034";
    public const string AssistanceItemCreate = "AUD-035";
    public const string AssistanceItemUpdate = "AUD-036";
    public const string SupplierDeactivate = "AUD-037";
    public const string SupplierRestore = "AUD-038";
    public const string OrganizationRestore = "AUD-017";
    public const string OrgUserRestore = "AUD-018";
    public const string OrgUserRoleChange = "AUD-019";
    public const string OrgUserPasswordReset = "AUD-020";
    public const string RoleCreate = "AUD-021";
    public const string RoleUpdate = "AUD-022";
    public const string RoleDisable = "AUD-023";
    public const string RoleRestore = "AUD-024";
    public const string SuperAdminEnterOrg = "AUD-025";
    public const string SuperAdminExitOrg = "AUD-026";
    public const string RoleGrantsUpdate = "AUD-016";
    public const string UserPermissionOverrideChange = "AUD-039";
    public const string AssistanceTypeRelatedSupplierAdd = "AUD-040";
    public const string AssistanceTypeRelatedSupplierRemove = "AUD-041";
}
