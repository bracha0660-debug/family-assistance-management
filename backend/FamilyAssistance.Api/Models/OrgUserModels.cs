namespace FamilyAssistance.Api.Models;

public sealed class CreateOrgUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid OrganizationRoleId { get; set; }
}

public sealed class UpdateOrgUserRequest
{
    public string? FullName { get; set; }
    public Guid? OrganizationRoleId { get; set; }
}

public sealed class DisableOrgUserRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class RestoreOrgUserRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class ResetOrgUserPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class OrgUserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public Guid? OrganizationRoleId { get; init; }
    public string? OrganizationRoleName { get; init; }
    public string Status { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsSelf { get; init; }
}

public sealed class OrgUserSummaryDto
{
    public int Total { get; init; }
    public int Active { get; init; }
    public int Disabled { get; init; }
}

public sealed class OrgUserListResponse
{
    public required OrgUserSummaryDto Summary { get; init; }
    public required IReadOnlyList<OrgUserDto> Users { get; init; }
}

public sealed class OrgUserResponse
{
    public required OrgUserDto User { get; init; }
}
