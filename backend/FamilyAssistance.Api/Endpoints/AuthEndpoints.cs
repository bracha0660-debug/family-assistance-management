using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Policies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
            (user.OrganizationId is not null && user.Organization?.Status != "active"))
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
        return Results.Ok(new LoginResponse { User = MapUser(user) });
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

    private static IResult Me(HttpContext httpContext)
    {
        var currentUser = httpContext.GetCurrentUser();
        if (currentUser is null)
        {
            return Results.Json(
                new ApiError { Error = "לא מחובר", Code = "UNAUTHORIZED" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(new LoginResponse
        {
            User = new UserDto
            {
                Id = currentUser.UserId,
                Username = currentUser.Username,
                FullName = currentUser.FullName,
                Role = currentUser.Role,
                OrganizationId = currentUser.OrganizationId,
                OrganizationName = currentUser.OrganizationName,
                OrganizationStatus = currentUser.OrganizationStatus
            }
        });
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

    private static UserDto MapUser(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Role = user.Role,
        OrganizationId = user.OrganizationId,
        OrganizationName = user.Organization?.Name,
        OrganizationStatus = user.Organization?.Status
    };

    private static void SetSessionCookie(HttpContext ctx, string name, string token, int idleHours, bool secure)
    {
        ctx.Response.Cookies.Append(name, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
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
            SameSite = SameSiteMode.Strict,
            Secure = secure,
            Path = "/",
            MaxAge = TimeSpan.Zero
        });
    }
}
