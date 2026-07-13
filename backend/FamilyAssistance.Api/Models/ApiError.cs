namespace FamilyAssistance.Api.Models;

public sealed class ApiError
{
    public required string Error { get; init; }
    public required string Code { get; init; }
    public object? Details { get; init; }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public Guid? OrganizationId { get; init; }
    public Guid? OrganizationRoleId { get; init; }
    public Guid? ActingOrganizationId { get; init; }
    public string? OrganizationName { get; init; }
    public string? OrganizationStatus { get; init; }
    public string? OrganizationLogoUrl { get; init; }
    public bool FullAccess { get; init; }
    public IReadOnlyList<UserGrantDto> Grants { get; init; } = [];
    public IReadOnlyList<UserGrantDto> RoleGrants { get; init; } = [];
    public IReadOnlyList<UserPermissionOverrideDto> Overrides { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed class LoginResponse
{
    public required UserDto User { get; init; }
    public string? SessionToken { get; init; }
}
