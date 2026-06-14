namespace FamilyAssistance.Api.Models;

public sealed class ApiError
{
    public required string Error { get; init; }
    public required string Code { get; init; }
    public IReadOnlyList<string> Details { get; init; } = [];
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
    public string? OrganizationName { get; init; }
    public string? OrganizationStatus { get; init; }
}

public sealed class LoginResponse
{
    public required UserDto User { get; init; }
    public string? SessionToken { get; init; }
}
