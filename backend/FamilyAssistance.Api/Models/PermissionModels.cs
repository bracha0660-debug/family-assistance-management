namespace FamilyAssistance.Api.Models;

public sealed class PermissionCatalogItemDto
{
    public required string PermissionKey { get; init; }
    public required string Category { get; init; }
    public required string DisplayNameHe { get; init; }
    public string? DescriptionHe { get; init; }
    public int SortOrder { get; init; }
    public bool SupportsMyRecords { get; init; }
    public bool ScopeApplies { get; init; }
}

public sealed class PermissionCatalogResponse
{
    public required IReadOnlyList<PermissionCatalogItemDto> Catalog { get; init; }
}

public sealed class RoleGrantDto
{
    public required string PermissionKey { get; init; }
    public required string Scope { get; init; }
}

public sealed class RoleGrantInputDto
{
    public string PermissionKey { get; set; } = string.Empty;
    public string Scope { get; set; } = "organization";
}

public sealed class OrganizationRoleListItemDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Status { get; init; }
    public string? FactoryPresetKey { get; init; }
    public int Version { get; init; }
    public int UserCount { get; init; }
}

public sealed class OrganizationRoleDetailDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Status { get; init; }
    public string? FactoryPresetKey { get; init; }
    public int Version { get; init; }
    public required IReadOnlyList<RoleGrantDto> Grants { get; init; }
}

public sealed class CreateOrganizationRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class UpdateOrganizationRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public sealed class UpdateRoleGrantsRequest
{
    public IReadOnlyList<RoleGrantInputDto> Grants { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public sealed class MaterialReasonRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class OrganizationRoleResponse
{
    public required OrganizationRoleDetailDto Role { get; init; }
}

public sealed class OrganizationRoleListResponse
{
    public required IReadOnlyList<OrganizationRoleListItemDto> Roles { get; init; }
}

public sealed class UserGrantDto
{
    public required string PermissionKey { get; init; }
    public required string Scope { get; init; }
}
