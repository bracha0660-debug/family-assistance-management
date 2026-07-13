namespace FamilyAssistance.Api.Models;

public sealed class UserPermissionOverrideDto
{
    public required string PermissionKey { get; init; }
    public required string Effect { get; init; }
    public string? Scope { get; init; }
}

public sealed class UserPermissionOverrideInputDto
{
    public string PermissionKey { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public string? Scope { get; set; }
}

public sealed class EffectiveGrantDto
{
    public required string PermissionKey { get; init; }
    public required string Scope { get; init; }
    public required string SourceTag { get; init; }
}

public sealed class UserPermissionOverridesResponse
{
    public required IReadOnlyList<RoleGrantDto> RoleGrants { get; init; }
    public required IReadOnlyList<UserPermissionOverrideDto> Overrides { get; init; }
    public required IReadOnlyList<EffectiveGrantDto> EffectiveGrants { get; init; }
}

public sealed class UpdateUserPermissionOverridesRequest
{
    public IReadOnlyList<UserPermissionOverrideInputDto> Overrides { get; set; } = [];
}
