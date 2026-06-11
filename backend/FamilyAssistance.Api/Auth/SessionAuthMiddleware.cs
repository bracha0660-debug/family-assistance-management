namespace FamilyAssistance.Api.Auth;

public class SessionAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SessionService sessionService)
    {
        var cookieName = sessionService.CookieName;
        context.Request.Cookies.TryGetValue(cookieName, out var rawToken);

        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            var session = await sessionService.GetValidSessionAsync(rawToken, context.RequestAborted);
            if (session is not null)
            {
                var user = session.User;
                context.SetCurrentUser(new CurrentUserContext
                {
                    UserId = user.Id,
                    Username = user.Username,
                    FullName = user.FullName,
                    Role = user.Role,
                    OrganizationId = user.OrganizationId,
                    OrganizationName = user.Organization?.Name,
                    OrganizationStatus = user.Organization?.Status,
                    SessionId = session.Id
                });
            }
        }

        await next(context);
    }
}

public static class SessionAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionAuth(this IApplicationBuilder app)
        => app.UseMiddleware<SessionAuthMiddleware>();
}
