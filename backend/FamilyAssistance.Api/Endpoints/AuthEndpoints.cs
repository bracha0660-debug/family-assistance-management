using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using FamilyAssistance.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", Login);
        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapGet("/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext httpContext,
        AppDbContext db,
        SessionService sessionService,
        ISecurityAuditService securityAudit,
        LoginRateLimiter rateLimiter,
        FamSessionOptions sessionOptions,
        PermissionService permissionService,
        CancellationToken cancellationToken)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var username = request.Username?.Trim() ?? string.Empty;

        var validationErrors = ValidateLoginRequest(username, request.Password);
        if (validationErrors.Count > 0)
        {
            return Results.Json(
                new ApiError { Error = validationErrors[0], Code = "VALIDATION_ERROR", Details = validationErrors },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (rateLimiter.IsRateLimited(username, ip))
        {
            try
            {
                await securityAudit.LogAsync(new SecurityAuditEntry
                {
                    EventCode = SecurityEventCodes.LoginFailedRateLimited,
                    EventType = SecurityEventTypes.LoginFailedRateLimited,
                    UsernameAttempted = username,
                    IpAddress = ip,
                    UserAgent = userAgent
                }, cancellationToken);
            }
            catch
            {
                return Results.Json(
                    new ApiError { Error = "שגיאת מערכת", Code = "INTERNAL_ERROR" },
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Json(
                new ApiError { Error = "יותר מדי ניסיונות. נסה שוב מאוחר יותר", Code = "RATE_LIMITED" },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var user = await db.Users
            .Include(u => u.Organization)
            .Include(u => u.OrganizationRole)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        var hasher = new PasswordHasher<User>();
        var passwordValid = user is not null &&
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) != PasswordVerificationResult.Failed;

        if (user is null || !passwordValid)
        {
            rateLimiter.RecordFailure(username, ip);
            try
            {
                await securityAudit.LogAsync(new SecurityAuditEntry
                {
                    EventCode = SecurityEventCodes.LoginFailedInvalidCredentials,
                    EventType = SecurityEventTypes.LoginFailedInvalidCredentials,
                    UsernameAttempted = username,
                    UserId = user?.Id,
                    OrganizationId = user?.OrganizationId,
                    IpAddress = ip,
                    UserAgent = userAgent
                }, cancellationToken);
            }
            catch
            {
                return Results.Json(
                    new ApiError { Error = "שגיאת מערכת", Code = "INTERNAL_ERROR" },
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Json(
                new ApiError { Error = "שם משתמש או סיסמה שגויים", Code = "INVALID_CREDENTIALS" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user.Status != "active" ||
            (user.OrganizationId is not null && user.Organization?.Status != "active") ||
            (user.Role == Roles.OrganizationUser && user.OrganizationRole is { Status: not "active" }))
        {
            try
            {
                await securityAudit.LogAsync(new SecurityAuditEntry
                {
                    EventCode = SecurityEventCodes.LoginFailedAccountInactive,
                    EventType = SecurityEventTypes.LoginFailedAccountInactive,
                    UsernameAttempted = username,
                    UserId = user.Id,
                    OrganizationId = user.OrganizationId,
                    IpAddress = ip,
                    UserAgent = userAgent
                }, cancellationToken);
            }
            catch
            {
                return Results.Json(
                    new ApiError { Error = "שגיאת מערכת", Code = "INTERNAL_ERROR" },
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Json(
                new ApiError { Error = "החשבון אינו פעיל", Code = "ACCOUNT_INACTIVE" },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var (session, rawToken) = await sessionService.CreateSessionAsync(user, ip, userAgent, cancellationToken);
        rateLimiter.ClearFailures(username, ip);

        try
        {
            await securityAudit.LogAsync(new SecurityAuditEntry
            {
                EventCode = SecurityEventCodes.LoginSuccess,
                EventType = SecurityEventTypes.LoginSuccess,
                UsernameAttempted = username,
                UserId = user.Id,
                OrganizationId = user.OrganizationId,
                SessionId = session.Id,
                IpAddress = ip,
                UserAgent = userAgent
            }, cancellationToken);
        }
        catch
        {
            await sessionService.RevokeSessionAsync(session.Id, cancellationToken);
            return Results.Json(
                new ApiError { Error = "שגיאת מערכת", Code = "INTERNAL_ERROR" },
                statusCode: StatusCodes.Status500InternalServerError);
        }

        SetSessionCookie(httpContext, sessionOptions.CookieName, rawToken, sessionOptions.IdleTimeoutHours, httpContext.Request.IsHttps);
        var userDtoBuilder = httpContext.RequestServices.GetRequiredService<UserDtoBuilder>();
        var userDto = await userDtoBuilder.BuildAsync(user, session, cancellationToken);
        return Results.Ok(new LoginResponse { User = userDto, SessionToken = rawToken });
    }

    private static async Task<IResult> Logout(
        HttpContext httpContext,
        SessionService sessionService,
        ISecurityAuditService securityAudit,
        FamSessionOptions sessionOptions,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser();
        if (currentUser is null)
        {
            return Results.Json(
                new ApiError { Error = "לא מחובר", Code = "UNAUTHORIZED" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        await sessionService.RevokeSessionAsync(currentUser.SessionId, cancellationToken);

        try
        {
            await securityAudit.LogAsync(new SecurityAuditEntry
            {
                EventCode = SecurityEventCodes.Logout,
                EventType = SecurityEventTypes.Logout,
                UsernameAttempted = currentUser.Username,
                UserId = currentUser.UserId,
                OrganizationId = currentUser.OrganizationId,
                SessionId = currentUser.SessionId,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext.Request.Headers.UserAgent.ToString()
            }, cancellationToken);
        }
        catch
        {
            return Results.Json(
                new ApiError { Error = "שגיאת מערכת", Code = "INTERNAL_ERROR" },
                statusCode: StatusCodes.Status500InternalServerError);
        }

        ClearSessionCookie(httpContext, sessionOptions.CookieName, httpContext.Request.IsHttps);
        return Results.NoContent();
    }

    private static async Task<IResult> Me(
        HttpContext httpContext,
        AppDbContext db,
        PermissionService permissionService,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetCurrentUser();
        if (currentUser is null)
        {
            return Results.Json(
                new ApiError { Error = "לא מחובר", Code = "UNAUTHORIZED" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await db.Users
            .Include(u => u.Organization)
            .Include(u => u.OrganizationRole)
            .FirstAsync(u => u.Id == currentUser.UserId, cancellationToken);

        var session = await db.UserSessions.FindAsync([currentUser.SessionId], cancellationToken);
        var userDtoBuilder = httpContext.RequestServices.GetRequiredService<UserDtoBuilder>();
        var userDto = await userDtoBuilder.BuildAsync(user, session, cancellationToken);

        return Results.Ok(new LoginResponse { User = userDto });
    }

    private static List<string> ValidateLoginRequest(string username, string password)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 100)
            errors.Add("שם משתמש הוא שדה חובה");
        if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 128)
            errors.Add("סיסמה היא שדה חובה");
        return errors;
    }

    private static void SetSessionCookie(HttpContext ctx, string name, string token, int idleHours, bool secure)
    {
        ctx.Response.Cookies.Append(name, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = secure,
            Path = "/",
            MaxAge = TimeSpan.FromHours(idleHours)
        });
    }

    private static void ClearSessionCookie(HttpContext ctx, string name, bool secure)
    {
        ctx.Response.Cookies.Append(name, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = secure,
            Path = "/",
            MaxAge = TimeSpan.Zero
        });
    }
}
