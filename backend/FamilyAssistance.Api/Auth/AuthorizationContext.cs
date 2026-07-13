namespace FamilyAssistance.Api.Auth;

public sealed class GrantContext
{
    public required string PermissionKey { get; init; }
    public required string Scope { get; init; }
}

public sealed class AuthorizationContext
{
    public required Guid UserId { get; init; }
    public required string SystemRole { get; init; }
    public Guid? OrganizationId { get; init; }
    public Guid? OrganizationRoleId { get; init; }
    public Guid? ActingOrganizationId { get; init; }
    public IReadOnlyList<GrantContext> Grants { get; init; } = [];

    public bool FullOrgAccess =>
        SystemRole == Constants.Roles.OrganizationAdministrator
        || (SystemRole == Constants.Roles.SuperAdmin && ActingOrganizationId is not null);

    public Guid? EffectiveOrganizationId =>
        SystemRole == Constants.Roles.SuperAdmin
            ? ActingOrganizationId
            : OrganizationId;

    public bool HasGrant(string permissionKey)
    {
        if (FullOrgAccess)
            return true;
        return Grants.Any(g => g.PermissionKey == permissionKey);
    }

    public GrantContext? GetGrant(string permissionKey) =>
        Grants.FirstOrDefault(g => g.PermissionKey == permissionKey);
}
