using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class OrganizationRoleService(
    AppDbContext db,
    IAuditService auditService)
{
    public async Task<IReadOnlyList<OrganizationRoleListItemDto>> ListRolesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await db.OrganizationRoles
            .Where(r => r.OrganizationId == organizationId)
            .OrderBy(r => r.Name)
            .Select(r => new OrganizationRoleListItemDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Status = r.Status,
                FactoryPresetKey = r.FactoryPresetKey,
                Version = r.Version,
                UserCount = r.Users.Count(u => u.Status == "active"),
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<OrganizationRoleDetailDto>> GetRoleAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await db.OrganizationRoles
            .Where(r => r.OrganizationId == organizationId && r.Id == roleId)
            .Select(r => new OrganizationRoleDetailDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Status = r.Status,
                FactoryPresetKey = r.FactoryPresetKey,
                Version = r.Version,
                Grants = r.Grants
                    .OrderBy(g => g.PermissionKey)
                    .Select(g => new RoleGrantDto
                    {
                        PermissionKey = g.PermissionKey,
                        Scope = g.Scope,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (role is null)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(404, "NOT_FOUND", "התפקיד לא נמצא");

        return ServiceResult<OrganizationRoleDetailDto>.Ok(role);
    }

    public async Task<ServiceResult<OrganizationRoleDetailDto>> CreateRoleAsync(
        Guid organizationId,
        CreateOrganizationRoleRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length < 2 || name.Length > 100)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", "שם התפקיד הוא שדה חובה");

        if (await db.OrganizationRoles.AnyAsync(
                r => r.OrganizationId == organizationId && r.Name == name, cancellationToken))
        {
            return ServiceResult<OrganizationRoleDetailDto>.Fail(409, "DUPLICATE_ROLE_NAME", "שם תפקיד כבר קיים");
        }

        var now = DateTime.UtcNow;
        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Description = request.Description?.Trim(),
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.OrganizationRoles.Add(role);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.RoleCreate,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "organization_role",
                EntityId = role.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new { role.Name, role.Description }),
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationRoleDetailDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return await GetRoleAsync(organizationId, role.Id, cancellationToken);
    }

    public async Task<ServiceResult<OrganizationRoleDetailDto>> UpdateRoleAsync(
        Guid organizationId,
        Guid roleId,
        UpdateOrganizationRoleRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var role = await db.OrganizationRoles
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Id == roleId, cancellationToken);
        if (role is null)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(404, "NOT_FOUND", "התפקיד לא נמצא");

        if (role.Version != expectedVersion)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var name = request.Name?.Trim();
        var description = request.Description?.Trim();
        var hasNameChange = !string.IsNullOrEmpty(name) && name != role.Name;
        var hasDescChange = description != role.Description;

        if (!hasNameChange && !hasDescChange)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        if (hasNameChange && (name!.Length < 2 || name.Length > 100))
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", "שם התפקיד הוא שדה חובה");

        if (hasNameChange && await db.OrganizationRoles.AnyAsync(
                r => r.OrganizationId == organizationId && r.Name == name && r.Id != roleId, cancellationToken))
        {
            return ServiceResult<OrganizationRoleDetailDto>.Fail(409, "DUPLICATE_ROLE_NAME", "שם תפקיד כבר קיים");
        }

        var oldName = role.Name;
        var oldDesc = role.Description;
        if (hasNameChange) role.Name = name!;
        if (hasDescChange) role.Description = description;
        role.Version++;
        role.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.RoleUpdate,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "organization_role",
                EntityId = role.Id,
                Action = "update",
                OldValue = JsonSerializer.Serialize(new { name = oldName, description = oldDesc }),
                NewValue = JsonSerializer.Serialize(new { name = role.Name, description = role.Description }),
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationRoleDetailDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return await GetRoleAsync(organizationId, roleId, cancellationToken);
    }

    public async Task<ServiceResult<OrganizationRoleDetailDto>> DisableRoleAsync(
        Guid organizationId,
        Guid roleId,
        MaterialReasonRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var role = await LoadRoleForMutation(organizationId, roleId, expectedVersion, cancellationToken);
        if (!role.IsSuccess)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(role.StatusCode, role.Code!, role.Error!);

        var entity = role.Value!;
        if (entity.Status == "disabled")
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        var reason = ValidateReason(request.Reason);
        if (reason is null)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");

        var hasUsers = await db.Users.AnyAsync(
            u => u.OrganizationRoleId == roleId && u.Status == "active", cancellationToken);
        if (hasUsers)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(409, "ROLE_HAS_USERS", "לא ניתן להשבית תפקיד עם משתמשים משויכים");

        entity.Status = "disabled";
        entity.Version++;
        entity.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.RoleDisable,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "organization_role",
                EntityId = entity.Id,
                Action = "disable",
                Reason = reason,
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationRoleDetailDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return await GetRoleAsync(organizationId, roleId, cancellationToken);
    }

    public async Task<ServiceResult<OrganizationRoleDetailDto>> RestoreRoleAsync(
        Guid organizationId,
        Guid roleId,
        MaterialReasonRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var role = await LoadRoleForMutation(organizationId, roleId, expectedVersion, cancellationToken);
        if (!role.IsSuccess)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(role.StatusCode, role.Code!, role.Error!);

        var entity = role.Value!;
        if (entity.Status == "active")
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        var reason = ValidateReason(request.Reason);
        if (reason is null)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");

        entity.Status = "active";
        entity.Version++;
        entity.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.RoleRestore,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "organization_role",
                EntityId = entity.Id,
                Action = "restore",
                Reason = reason,
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationRoleDetailDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return await GetRoleAsync(organizationId, roleId, cancellationToken);
    }

    public async Task<ServiceResult<OrganizationRoleDetailDto>> UpdateGrantsAsync(
        Guid organizationId,
        Guid roleId,
        UpdateRoleGrantsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var reason = ValidateReason(request.Reason);
        if (reason is null)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");

        var role = await db.OrganizationRoles
            .Include(r => r.Grants)
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Id == roleId, cancellationToken);
        if (role is null)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(404, "NOT_FOUND", "התפקיד לא נמצא");

        var catalog = await db.PermissionCatalog
            .Where(p => p.IsActive)
            .ToDictionaryAsync(p => p.PermissionKey, cancellationToken);

        var requested = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var grant in request.Grants)
        {
            if (string.IsNullOrWhiteSpace(grant.PermissionKey))
                continue;

            var key = grant.PermissionKey.Trim();
            if (!catalog.TryGetValue(key, out var cat))
                return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", "הרשאה לא חוקית", [key]);

            var scope = grant.Scope?.Trim() ?? PermissionScopes.Organization;
            if (!PermissionService.ValidateGrantScope(cat, scope))
                return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", "היקף הרשאה לא חוקי", [key, scope]);

            requested[key] = scope;
        }

        var current = role.Grants.ToDictionary(g => g.PermissionKey, g => g.Scope, StringComparer.Ordinal);
        if (current.Count == requested.Count && current.All(kv => requested.TryGetValue(kv.Key, out var s) && s == kv.Value))
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        var added = requested.Keys.Except(current.Keys).OrderBy(k => k).ToList();
        var removed = current.Keys.Except(requested.Keys).OrderBy(k => k).ToList();
        var changed = requested.Keys.Intersect(current.Keys)
            .Where(k => requested[k] != current[k])
            .OrderBy(k => k)
            .ToList();

        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (removed.Count > 0)
            {
                var toRemove = role.Grants.Where(g => removed.Contains(g.PermissionKey)).ToList();
                db.OrganizationRoleGrants.RemoveRange(toRemove);
            }

            foreach (var key in added)
            {
                db.OrganizationRoleGrants.Add(new OrganizationRoleGrant
                {
                    Id = Guid.NewGuid(),
                    OrganizationRoleId = roleId,
                    PermissionKey = key,
                    Scope = requested[key],
                    GrantedAt = now,
                    GrantedByUserId = actorUserId,
                });
            }

            foreach (var key in changed)
            {
                var grant = role.Grants.First(g => g.PermissionKey == key);
                grant.Scope = requested[key];
                grant.GrantedAt = now;
                grant.GrantedByUserId = actorUserId;
            }

            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.RoleGrantsUpdate,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "organization_role",
                EntityId = roleId,
                Action = "grants_update",
                NewValue = JsonSerializer.Serialize(new
                {
                    added,
                    removed,
                    changed,
                    finalGrants = requested.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value),
                }),
                Reason = reason,
            });

            role.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationRoleDetailDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return await GetRoleAsync(organizationId, roleId, cancellationToken);
    }

    public async Task<ServiceResult<OrganizationRoleDetailDto>> ResetGrantsAsync(
        Guid organizationId,
        Guid roleId,
        MaterialReasonRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var role = await db.OrganizationRoles
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Id == roleId, cancellationToken);
        if (role is null)
            return ServiceResult<OrganizationRoleDetailDto>.Fail(404, "NOT_FOUND", "התפקיד לא נמצא");

        if (string.IsNullOrWhiteSpace(role.FactoryPresetKey))
            return ServiceResult<OrganizationRoleDetailDto>.Fail(400, "VALIDATION_ERROR", "לא ניתן לאפס תפקיד מותאם אישית");

        var seed = RoleTemplateSeed.GrantsForPreset(role.FactoryPresetKey);
        return await UpdateGrantsAsync(
            organizationId,
            roleId,
            new UpdateRoleGrantsRequest
            {
                Reason = request.Reason,
                Grants = seed.Select(s => new RoleGrantInputDto
                {
                    PermissionKey = s.PermissionKey,
                    Scope = s.Scope,
                }).ToList(),
            },
            actorUserId,
            cancellationToken);
    }

    private async Task<ServiceResult<OrganizationRole>> LoadRoleForMutation(
        Guid organizationId,
        Guid roleId,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is null)
            return ServiceResult<OrganizationRole>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var role = await db.OrganizationRoles
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Id == roleId, cancellationToken);
        if (role is null)
            return ServiceResult<OrganizationRole>.Fail(404, "NOT_FOUND", "התפקיד לא נמצא");

        if (role.Version != expectedVersion)
            return ServiceResult<OrganizationRole>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        return ServiceResult<OrganizationRole>.Ok(role);
    }

    private static string? ValidateReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        return trimmed.Length is >= 3 and <= 500 ? trimmed : null;
    }
}
