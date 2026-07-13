using System.Security.Cryptography;
using System.Text;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Auth;

public class SessionService(AppDbContext db, FamSessionOptions options)
{
    public string CookieName => options.CookieName;

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public async Task<(UserSession Session, string RawToken)> CreateSessionAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var token = GenerateToken();
        var now = DateTime.UtcNow;
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SessionTokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.AddHours(options.IdleTimeoutHours),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return (session, token);
    }

    public async Task<UserSession?> GetValidSessionAsync(string? rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var hash = HashToken(rawToken);
        var session = await db.UserSessions
            .Include(s => s.User)
            .ThenInclude(u => u.Organization)
            .Include(s => s.User)
            .ThenInclude(u => u.OrganizationRole)
            .FirstOrDefaultAsync(s => s.SessionTokenHash == hash, cancellationToken);

        if (session is null || session.RevokedAt is not null)
            return null;

        var now = DateTime.UtcNow;
        if (session.ExpiresAt <= now)
            return null;

        if (session.CreatedAt.AddHours(options.AbsoluteTimeoutHours) <= now)
            return null;

        if (session.User.Status != "active")
            return null;

        if (session.User.OrganizationId is not null &&
            (session.User.Organization is null || session.User.Organization.Status != "active"))
            return null;

        session.ExpiresAt = now.AddHours(options.IdleTimeoutHours);
        var absoluteMax = session.CreatedAt.AddHours(options.AbsoluteTimeoutHours);
        if (session.ExpiresAt > absoluteMax)
            session.ExpiresAt = absoluteMax;

        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.FindAsync([sessionId], cancellationToken);
        if (session is null || session.RevokedAt is not null)
            return;

        session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeOrganizationSessionsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await db.UserSessions
            .Where(s => s.RevokedAt == null && s.User.OrganizationId == organizationId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAt, now),
                cancellationToken);
    }

    public async Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await db.UserSessions
            .Where(s => s.RevokedAt == null && s.UserId == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAt, now),
                cancellationToken);
    }

    public async Task SetActingOrganizationAsync(
        Guid sessionId,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.FindAsync([sessionId], cancellationToken);
        if (session is null)
            return;

        session.ActingOrganizationId = organizationId;
        await db.SaveChangesAsync(cancellationToken);
    }
}
