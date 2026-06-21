namespace FamilyAssistance.Api.Models;

public sealed class CreateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public sealed class SuspendOrganizationRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class RestoreOrganizationRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class BootstrapOrgAdminRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public sealed class OrganizationDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool HasOrgAdmin { get; init; }
}

public sealed class OrganizationSummaryDto
{
    public int Total { get; init; }
    public int Active { get; init; }
    public int Suspended { get; init; }
}

public sealed class OrganizationListResponse
{
    public required OrganizationSummaryDto Summary { get; init; }
    public required IReadOnlyList<OrganizationDto> Organizations { get; init; }
}

public sealed class OrganizationResponse
{
    public required OrganizationDto Organization { get; init; }
}

public sealed class BootstrapUserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public Guid OrganizationId { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class BootstrapUserResponse
{
    public required BootstrapUserDto User { get; init; }
}
