using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class OrganizationUserService(
    AppDbContext db,
    IAuditService auditService,
    SessionService sessionService)
{
    private static readonly HashSet<string> AssignableRoles =
    [
        Roles.Coordinator,
        Roles.Manager,
        Roles.Finance
    ];

    public async Task<OrgUserListResponse> ListUsersAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .Where(u => u.OrganizationId == organizationId)
            .OrderBy(u => u.FullName)
            .Select(u => new OrgUserDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                Status = u.Status,
                Version = u.Version,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                IsSelf = u.Id == currentUserId
            })
            .ToListAsync(cancellationToken);

        var total = users.Count;
        var active = users.Count(u => u.Status == "active");
        var disabled = users.Count(u => u.Status == "disabled");

        return new OrgUserListResponse
        {
            Summary = new OrgUserSummaryDto
            {
                Total = total,
                Active = active,
                Disabled = disabled
            },
            Users = users
        };
    }

    public async Task<ServiceResult<OrgUserDto>> CreateUserAsync(
        Guid organizationId,
        CreateOrgUserRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var role = request.Role?.Trim() ?? string.Empty;
        if (role.Length > 0 && !AssignableRoles.Contains(role))
            return ServiceResult<OrgUserDto>.Fail(400, "INVALID_ROLE", "תפקיד לא חוקי לשלב זה");

        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var org = await db.Organizations.FindAsync([organizationId], cancellationToken);
        if (org is null)
            return ServiceResult<OrgUserDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");

        if (org.Status != "active")
            return ServiceResult<OrgUserDto>.Fail(409, "ORG_SUSPENDED", "הארגון אינו פעיל");

        var username = request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            return ServiceResult<OrgUserDto>.Fail(409, "DUPLICATE_USERNAME", "שם משתמש כבר קיים");

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Username = username,
            FullName = request.FullName.Trim(),
            Role = request.Role.Trim(),
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Users.Add(user);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.OrgUserCreate,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "user",
                EntityId = user.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new
                {
                    user.Username,
                    user.FullName,
                    user.Role,
                    user.OrganizationId
                })
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrgUserDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<OrgUserDto>.Ok(MapUser(user, isSelf: false));
    }

    public async Task<ServiceResult<OrgUserDto>> UpdateUserAsync(
        Guid organizationId,
        Guid userId,
        UpdateOrgUserRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<OrgUserDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId, cancellationToken);
        if (user is null)
            return ServiceResult<OrgUserDto>.Fail(404, "NOT_FOUND", "המשתמש לא נמצא");

        if (user.Status != "active")
            return ServiceResult<OrgUserDto>.Fail(409, "USER_DISABLED", "המשתמש מושבת");

        if (user.Version != expectedVersion)
            return ServiceResult<OrgUserDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var newFullName = request.FullName?.Trim();
        var newRole = request.Role?.Trim();

        var hasFullNameChange = !string.IsNullOrEmpty(newFullName) && newFullName != user.FullName;
        var hasRoleChange = !string.IsNullOrEmpty(newRole) && newRole != user.Role;

        if (!hasFullNameChange && !hasRoleChange)
            return ServiceResult<OrgUserDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        if (hasFullNameChange && (newFullName!.Length < 2 || newFullName.Length > 200))
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", "שם מלא הוא שדה חובה");

        if (hasRoleChange)
        {
            if (user.Role == Roles.OrganizationAdministrator)
                return ServiceResult<OrgUserDto>.Fail(400, "ORG_ADMIN_ROLE_LOCKED",
                    "אין אפשרות לשנות תפקיד של מנהל ארגון בשלב זה");

            if (!AssignableRoles.Contains(newRole!))
                return ServiceResult<OrgUserDto>.Fail(400, "INVALID_ROLE", "תפקיד לא חוקי לשלב זה");

            if (user.Id == actorUserId)
                return ServiceResult<OrgUserDto>.Fail(403, "SELF_ROLE_CHANGE",
                    "אין אפשרות לשנות את התפקיד של עצמך");
        }

        var oldFullName = user.FullName;
        var oldRole = user.Role;
        var now = DateTime.UtcNow;

        if (hasFullNameChange)
            user.FullName = newFullName!;
        if (hasRoleChange)
            user.Role = newRole!;
        user.Version++;
        user.UpdatedAt = now;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (hasFullNameChange)
            {
                auditService.Stage(new AuditEntry
                {
                    EventCode = BusinessEventCodes.OrgUserUpdate,
                    OrganizationId = organizationId,
                    ActorUserId = actorUserId,
                    EntityType = "user",
                    EntityId = user.Id,
                    Action = "update",
                    FieldName = "full_name",
                    OldValue = oldFullName,
                    NewValue = user.FullName
                });
            }
            if (hasRoleChange)
            {
                auditService.Stage(new AuditEntry
                {
                    EventCode = BusinessEventCodes.OrgUserUpdate,
                    OrganizationId = organizationId,
                    ActorUserId = actorUserId,
                    EntityType = "user",
                    EntityId = user.Id,
                    Action = "update",
                    FieldName = "role",
                    OldValue = oldRole,
                    NewValue = user.Role
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrgUserDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<OrgUserDto>.Ok(MapUser(user, isSelf: user.Id == actorUserId));
    }

    public async Task<ServiceResult<OrgUserDto>> DisableUserAsync(
        Guid organizationId,
        Guid userId,
        DisableOrgUserRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<OrgUserDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId, cancellationToken);
        if (user is null)
            return ServiceResult<OrgUserDto>.Fail(404, "NOT_FOUND", "המשתמש לא נמצא");

        if (user.Status == "disabled")
            return ServiceResult<OrgUserDto>.Fail(409, "ALREADY_DISABLED", "המשתמש כבר מושבת");

        if (user.Id == actorUserId)
            return ServiceResult<OrgUserDto>.Fail(403, "SELF_DISABLE",
                "אין אפשרות להשבית את החשבון שלך");

        if (user.Version != expectedVersion)
            return ServiceResult<OrgUserDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        if (user.Role == Roles.OrganizationAdministrator)
        {
            var otherActiveOrgAdmins = await db.Users
                .CountAsync(
                    u => u.OrganizationId == organizationId
                        && u.Id != user.Id
                        && u.Role == Roles.OrganizationAdministrator
                        && u.Status == "active",
                    cancellationToken);
            if (otherActiveOrgAdmins == 0)
                return ServiceResult<OrgUserDto>.Fail(409, "LAST_ORG_ADMIN",
                    "לא ניתן להשבית את מנהל הארגון היחיד הפעיל");
        }

        if (user.Role == Roles.Coordinator)
        {
            var activeFamilies = await db.Families
                .CountAsync(
                    f => f.OrganizationId == organizationId
                        && f.AssignedCoordinatorId == user.Id
                        && f.Status == "active",
                    cancellationToken);
            if (activeFamilies > 0)
                return ServiceResult<OrgUserDto>.Fail(409, "COORDINATOR_HAS_ACTIVE_FAMILIES",
                    $"לא ניתן להשבית מתאם/ת עם משפחות פעילות ({activeFamilies}). יש להעביר או להשבית את המשפחות תחילה.");
        }

        var oldStatus = user.Status;
        user.Status = "disabled";
        user.Version++;
        user.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.OrgUserDisable,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "user",
                EntityId = user.Id,
                Action = "user_disable",
                FieldName = "status",
                OldValue = oldStatus,
                NewValue = "disabled",
                Reason = reason
            });
            await db.SaveChangesAsync(cancellationToken);
            await sessionService.RevokeUserSessionsAsync(user.Id, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrgUserDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<OrgUserDto>.Ok(MapUser(user, isSelf: false));
    }

    private static List<string> ValidateCreateRequest(CreateOrgUserRequest request)
    {
        var errors = new List<string>();
        var username = request.Username?.Trim() ?? string.Empty;
        if (username.Length < 3 || username.Length > 100)
            errors.Add("שם משתמש הוא שדה חובה");

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8 || request.Password.Length > 128)
            errors.Add("סיסמה היא שדה חובה");

        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length < 2 || fullName.Length > 200)
            errors.Add("שם מלא הוא שדה חובה");

        var role = request.Role?.Trim() ?? string.Empty;
        if (role.Length == 0)
            errors.Add("תפקיד הוא שדה חובה");

        return errors;
    }

    private static OrgUserDto MapUser(User user, bool isSelf) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Role = user.Role,
        Status = user.Status,
        Version = user.Version,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        IsSelf = isSelf
    };
}
