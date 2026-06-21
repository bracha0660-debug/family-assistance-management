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
                OrganizationRoleId = u.OrganizationRoleId,
                OrganizationRoleName = u.OrganizationRole != null ? u.OrganizationRole.Name : null,
                Status = u.Status,
                Version = u.Version,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                IsSelf = u.Id == currentUserId,
            })
            .ToListAsync(cancellationToken);

        return new OrgUserListResponse
        {
            Summary = new OrgUserSummaryDto
            {
                Total = users.Count,
                Active = users.Count(u => u.Status == "active"),
                Disabled = users.Count(u => u.Status == "disabled"),
            },
            Users = users,
        };
    }

    public async Task<ServiceResult<OrgUserDto>> CreateUserAsync(
        Guid organizationId,
        CreateOrgUserRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var orgRole = await db.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Id == request.OrganizationRoleId
                && r.OrganizationId == organizationId, cancellationToken);
        if (orgRole is null || orgRole.Status != "active")
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", "תפקיד לא חוקי");

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
            Role = Roles.OrganizationUser,
            OrganizationRoleId = request.OrganizationRoleId,
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
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
                    user.OrganizationRoleId,
                }),
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrgUserDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<OrgUserDto>.Ok(await MapUserAsync(user.Id, actorUserId, cancellationToken));
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
            .Include(u => u.OrganizationRole)
            .FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId, cancellationToken);
        if (user is null)
            return ServiceResult<OrgUserDto>.Fail(404, "NOT_FOUND", "המשתמש לא נמצא");
        if (user.Status != "active")
            return ServiceResult<OrgUserDto>.Fail(409, "USER_DISABLED", "המשתמש מושבת");
        if (user.Version != expectedVersion)
            return ServiceResult<OrgUserDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var newFullName = request.FullName?.Trim();
        var hasFullNameChange = !string.IsNullOrEmpty(newFullName) && newFullName != user.FullName;
        var hasRoleChange = request.OrganizationRoleId is not null
            && request.OrganizationRoleId != user.OrganizationRoleId;

        if (!hasFullNameChange && !hasRoleChange)
            return ServiceResult<OrgUserDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        if (hasFullNameChange && (newFullName!.Length < 2 || newFullName.Length > 200))
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", "שם מלא הוא שדה חובה");

        OrganizationRole? newOrgRole = null;
        if (hasRoleChange)
        {
            if (user.Role == Roles.OrganizationAdministrator)
            {
                var guard = await ValidateLastOrgAdminDemoteAsync(organizationId, user.Id, cancellationToken);
                if (guard is not null)
                    return guard;
            }
            else if (user.Id == actorUserId)
            {
                return ServiceResult<OrgUserDto>.Fail(403, "SELF_ROLE_CHANGE", "אין אפשרות לשנות את התפקיד של עצמך");
            }

            newOrgRole = await db.OrganizationRoles
                .FirstOrDefaultAsync(r => r.Id == request.OrganizationRoleId
                    && r.OrganizationId == organizationId, cancellationToken);
            if (newOrgRole is null || newOrgRole.Status != "active")
                return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", "תפקיד לא חוקי");
        }

        var oldFullName = user.FullName;
        var oldRoleId = user.OrganizationRoleId;
        if (hasFullNameChange)
            user.FullName = newFullName!;
        if (hasRoleChange)
        {
            user.OrganizationRoleId = request.OrganizationRoleId;
            user.Role = Roles.OrganizationUser;
        }
        user.Version++;
        user.UpdatedAt = DateTime.UtcNow;

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
                    NewValue = user.FullName,
                });
            }
            if (hasRoleChange)
            {
                auditService.Stage(new AuditEntry
                {
                    EventCode = BusinessEventCodes.OrgUserRoleChange,
                    OrganizationId = organizationId,
                    ActorUserId = actorUserId,
                    EntityType = "user",
                    EntityId = user.Id,
                    Action = "role_change",
                    OldValue = oldRoleId?.ToString(),
                    NewValue = user.OrganizationRoleId?.ToString(),
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

        return ServiceResult<OrgUserDto>.Ok(await MapUserAsync(user.Id, actorUserId, cancellationToken));
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
            return ServiceResult<OrgUserDto>.Fail(403, "SELF_DISABLE", "אין אפשרות להשבית את החשבון שלך");
        if (user.Version != expectedVersion)
            return ServiceResult<OrgUserDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        if (user.Role == Roles.OrganizationAdministrator)
        {
            var guard = await ValidateLastOrgAdminDisableAsync(organizationId, user.Id, cancellationToken);
            if (guard is not null)
                return guard;
        }

        var activeFamilies = await db.Families
            .CountAsync(f => f.OrganizationId == organizationId
                && f.AssignedCoordinatorId == user.Id
                && f.Status == "active", cancellationToken);
        if (activeFamilies > 0)
        {
            return ServiceResult<OrgUserDto>.Fail(409, "COORDINATOR_HAS_ACTIVE_FAMILIES",
                $"לא ניתן להשבית משתמש עם משפחות פעילות ({activeFamilies}). יש להעביר או להשבית את המשפחות תחילה.");
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
                Reason = reason,
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

        return ServiceResult<OrgUserDto>.Ok(await MapUserAsync(user.Id, actorUserId, cancellationToken));
    }

    public async Task<ServiceResult<OrgUserDto>> RestoreUserAsync(
        Guid organizationId,
        Guid userId,
        RestoreOrgUserRequest request,
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
        if (user.Status == "active")
            return ServiceResult<OrgUserDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");
        if (user.Version != expectedVersion)
            return ServiceResult<OrgUserDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        if (user.OrganizationRoleId is not null)
        {
            var roleActive = await db.OrganizationRoles
                .AnyAsync(r => r.Id == user.OrganizationRoleId && r.Status == "active", cancellationToken);
            if (!roleActive)
                return ServiceResult<OrgUserDto>.Fail(409, "ROLE_DISABLED", "התפקיד המשויך אינו פעיל");
        }

        user.Status = "active";
        user.Version++;
        user.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.OrgUserRestore,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "user",
                EntityId = user.Id,
                Action = "restore",
                Reason = reason,
            });
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

        return ServiceResult<OrgUserDto>.Ok(await MapUserAsync(user.Id, actorUserId, cancellationToken));
    }

    public async Task<ServiceResult<OrgUserDto>> ResetPasswordAsync(
        Guid organizationId,
        Guid userId,
        ResetOrgUserPasswordRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8 || request.NewPassword.Length > 128)
            return ServiceResult<OrgUserDto>.Fail(400, "VALIDATION_ERROR", "סיסמה היא שדה חובה");

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId, cancellationToken);
        if (user is null)
            return ServiceResult<OrgUserDto>.Fail(404, "NOT_FOUND", "המשתמש לא נמצא");

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.Version++;
        user.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.OrgUserPasswordReset,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "user",
                EntityId = user.Id,
                Action = "password_reset",
                Reason = reason,
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

        return ServiceResult<OrgUserDto>.Ok(await MapUserAsync(user.Id, actorUserId, cancellationToken));
    }

    public async Task<ServiceResult<OrgUserDto>?> ValidateLastOrgAdminDisableAsync(
        Guid organizationId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var otherActive = await CountActiveOrgAdminsAsync(organizationId, targetUserId, cancellationToken);
        if (otherActive == 0)
            return ServiceResult<OrgUserDto>.Fail(409, "LAST_ORG_ADMIN", "לא ניתן להשבית את מנהל הארגון היחיד הפעיל");
        return null;
    }

    public async Task<ServiceResult<OrgUserDto>?> ValidateLastOrgAdminDemoteAsync(
        Guid organizationId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var otherActive = await CountActiveOrgAdminsAsync(organizationId, targetUserId, cancellationToken);
        if (otherActive == 0)
            return ServiceResult<OrgUserDto>.Fail(409, "LAST_ORG_ADMIN", "לא ניתן לשנות את התפקיד של מנהל הארגון היחיד הפעיל");
        return null;
    }

    private static async Task<int> CountActiveOrgAdminsAsync(
        AppDbContext db,
        Guid organizationId,
        Guid excludeUserId,
        CancellationToken cancellationToken) =>
        await db.Users.CountAsync(
            u => u.OrganizationId == organizationId
                && u.Id != excludeUserId
                && u.Role == Roles.OrganizationAdministrator
                && u.Status == "active",
            cancellationToken);

    private async Task<int> CountActiveOrgAdminsAsync(
        Guid organizationId,
        Guid excludeUserId,
        CancellationToken cancellationToken) =>
        await CountActiveOrgAdminsAsync(db, organizationId, excludeUserId, cancellationToken);

    private static List<string> ValidateCreateRequest(CreateOrgUserRequest request)
    {
        var errors = new List<string>();
        if ((request.Username?.Trim() ?? string.Empty).Length is < 3 or > 100)
            errors.Add("שם משתמש הוא שדה חובה");
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8 || request.Password.Length > 128)
            errors.Add("סיסמה הוא שדה חובה");
        if ((request.FullName?.Trim() ?? string.Empty).Length is < 2 or > 200)
            errors.Add("שם מלא הוא שדה חובה");
        if (request.OrganizationRoleId == Guid.Empty)
            errors.Add("תפקיד הוא שדה חובה");
        return errors;
    }

    private async Task<OrgUserDto> MapUserAsync(Guid userId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.OrganizationRole)
            .FirstAsync(u => u.Id == userId, cancellationToken);
        return new OrgUserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            OrganizationRoleId = user.OrganizationRoleId,
            OrganizationRoleName = user.OrganizationRole?.Name,
            Status = user.Status,
            Version = user.Version,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsSelf = user.Id == actorUserId,
        };
    }
}
