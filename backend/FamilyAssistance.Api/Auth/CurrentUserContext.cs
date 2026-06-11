namespace FamilyAssistance.Api.Auth;

public sealed class CurrentUserContext
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public Guid? OrganizationId { get; init; }
    public string? OrganizationName { get; init; }
    public string? OrganizationStatus { get; init; }
    public Guid SessionId { get; init; }
}

public static class HttpContextCurrentUserExtensions
{
    private const string Key = "CurrentUser";

    public static CurrentUserContext? GetCurrentUser(this HttpContext context)
        => context.Items[Key] as CurrentUserContext;

    public static void SetCurrentUser(this HttpContext context, CurrentUserContext user)
        => context.Items[Key] = user;
}
