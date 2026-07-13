using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class UserPermissionOverrideService(
    AppDbContext db,
    PermissionService permissionService,
    IAuditService auditService)
{
    public async Task<ServiceResult<UserPermissionOverridesResponse>> GetAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadTargetUserAsync(organizationId, userId, cancellationToken);
        if (!user.IsSuccess)
            return ServiceResult<UserPermissionOverridesResponse>.Fail(user.StatusCode, user.Code!, user.Error!);

        return ServiceResult<UserPermissionOverridesResponse>.Ok(
            await BuildResponseAsync(user.Value!, cancellationToken));
    }

    public async Task<ServiceResult<UserPermissionOverridesResponse>> ReplaceAsync(
        Guid organizationId,
        Guid userId,
        UpdateUserPermissionOverridesRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadTargetUserAsync(organizationId, userId, cancellationToken);
        if (!user.IsSuccess)
            return ServiceResult<UserPermissionOverridesResponse>.Fail(user.StatusCode, user.Code!, user.Error!);

        var target = user.Value!;
        var catalog = await db.PermissionCatalog
            .Where(p => p.IsActive)
            .ToDictionaryAsync(p => p.PermissionKey, cancellationToken);

        var requested = new Dictionary<string, (string Effect, string? Scope)>(StringComparer.Ordinal);
        foreach (var item in request.Overrides)
        {
            if (string.IsNullOrWhiteSpace(item.PermissionKey))
                continue;

            var key = item.PermissionKey.Trim();
            if (!PermissionKeys.IsUserOverrideAssignable(key))
                return ServiceResult<UserPermissionOverridesResponse>.Fail(
                    400, "VALIDATION_ERROR", "הרשאה לא ניתנת להתאמה אישית", [key]);

            if (requested.ContainsKey(key))
                return ServiceResult<UserPermissionOverridesResponse>.Fail(
                    400, "VALIDATION_ERROR", "הרשאה כפולה בבקשה", [key]);

            if (!catalog.TryGetValue(key, out var cat))
                return ServiceResult<UserPermissionOverridesResponse>.Fail(
                    400, "VALIDATION_ERROR", "הרשאה לא חוקית", [key]);

            var effect = item.Effect?.Trim() ?? string.Empty;
            if (!PermissionOverrideEffects.All.Contains(effect))
                return ServiceResult<UserPermissionOverridesResponse>.Fail(
                    400, "VALIDATION_ERROR", "סוג התאמה לא חוקי", [key, effect]);

            if (effect == PermissionOverrideEffects.Deny)
            {
                if (!string.IsNullOrWhiteSpace(item.Scope))
                    return ServiceResult<UserPermissionOverridesResponse>.Fail(
                        400, "VALIDATION_ERROR", "שלילה אינה כוללת היקף", [key]);
                requested[key] = (effect, null);
                continue;
            }

            var scope = item.Scope?.Trim() ?? PermissionScopes.Organization;
            if (!PermissionService.ValidateGrantScope(cat, scope))
                return ServiceResult<UserPermissionOverridesResponse>.Fail(
                    400, "VALIDATION_ERROR", "היקף הרשאה לא חוקי", [key, scope]);

            requested[key] = (effect, scope);
        }

        var existing = await db.UserPermissionOverrides
            .Where(o => o.UserId == userId)
            .ToListAsync(cancellationToken);
        var existingMap = existing.ToDictionary(o => o.PermissionKey, StringComparer.Ordinal);

        var allKeys = existingMap.Keys.Union(requested.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();
        var auditTransitions = new List<(string Key, UserPermissionOverride? Old, (string Effect, string? Scope)? New)>();
        foreach (var key in allKeys)
        {
            existingMap.TryGetValue(key, out var oldRow);
            requested.TryGetValue(key, out var newRow);
            var oldState = PermissionService.EncodeOverrideState(oldRow);
            var newState = newRow == default
                ? "none"
                : newRow.Effect == PermissionOverrideEffects.Deny
                    ? "deny"
                    : $"grant:{newRow.Scope}";

            if (oldState != newState)
                auditTransitions.Add((key, oldRow, newRow == default ? null : newRow));
        }

        if (auditTransitions.Count == 0)
            return ServiceResult<UserPermissionOverridesResponse>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (existing.Count > 0)
                db.UserPermissionOverrides.RemoveRange(existing);

            foreach (var (key, entry) in requested)
            {
                db.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    UserId = userId,
                    PermissionKey = key,
                    Effect = entry.Effect,
                    Scope = entry.Effect == PermissionOverrideEffects.Deny ? null : entry.Scope,
                    GrantedByUserId = actorUserId,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            foreach (var (key, oldRow, newRow) in auditTransitions)
            {
                auditService.Stage(new AuditEntry
                {
                    EventCode = BusinessEventCodes.UserPermissionOverrideChange,
                    OrganizationId = organizationId,
                    ActorUserId = actorUserId,
                    EntityType = "user",
                    EntityId = userId,
                    Action = "permission_override_change",
                    FieldName = key,
                    OldValue = PermissionService.EncodeOverrideState(oldRow),
                    NewValue = newRow is null
                        ? "none"
                        : newRow.Value.Effect == PermissionOverrideEffects.Deny
                            ? "deny"
                            : $"grant:{newRow.Value.Scope}",
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<UserPermissionOverridesResponse>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<UserPermissionOverridesResponse>.Ok(
            await BuildResponseAsync(target, cancellationToken));
    }

    public async Task<ServiceResult<UserPermissionOverridesResponse>> DeleteOneAsync(
        Guid organizationId,
        Guid userId,
        string permissionKey,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadTargetUserAsync(organizationId, userId, cancellationToken);
        if (!user.IsSuccess)
            return ServiceResult<UserPermissionOverridesResponse>.Fail(user.StatusCode, user.Code!, user.Error!);

        var key = permissionKey.Trim();
        var existing = await db.UserPermissionOverrides
            .FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionKey == key, cancellationToken);
        if (existing is null)
            return ServiceResult<UserPermissionOverridesResponse>.Fail(404, "NOT_FOUND", "התאמה לא נמצאה");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.UserPermissionOverrides.Remove(existing);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.UserPermissionOverrideChange,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "user",
                EntityId = userId,
                Action = "permission_override_change",
                FieldName = key,
                OldValue = PermissionService.EncodeOverrideState(existing),
                NewValue = "none",
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<UserPermissionOverridesResponse>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<UserPermissionOverridesResponse>.Ok(
            await BuildResponseAsync(user.Value!, cancellationToken));
    }

    private async Task<ServiceResult<User>> LoadTargetUserAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId, cancellationToken);
        if (user is null)
            return ServiceResult<User>.Fail(404, "NOT_FOUND", "המשתמש לא נמצא");

        if (user.Status != "active")
            return ServiceResult<User>.Fail(400, "VALIDATION_ERROR", "לא ניתן לערוך משתמש מושבת");

        if (user.Role != Roles.OrganizationUser)
            return ServiceResult<User>.Fail(400, "VALIDATION_ERROR", "לא ניתן להתאים הרשאות למשתמש זה");

        return ServiceResult<User>.Ok(user);
    }

    private async Task<UserPermissionOverridesResponse> BuildResponseAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var roleGrants = await permissionService.GetRoleGrantMapAsync(user.OrganizationRoleId, cancellationToken);
        var overrides = await db.UserPermissionOverrides
            .Where(o => o.UserId == user.Id)
            .OrderBy(o => o.PermissionKey)
            .ToListAsync(cancellationToken);

        var effectiveMap = PermissionService.ComputeEffectiveGrants(roleGrants, overrides);
        var overrideMap = overrides.ToDictionary(o => o.PermissionKey, StringComparer.Ordinal);

        return new UserPermissionOverridesResponse
        {
            RoleGrants = roleGrants
                .OrderBy(kv => kv.Key)
                .Select(kv => new RoleGrantDto { PermissionKey = kv.Key, Scope = kv.Value })
                .ToList(),
            Overrides = overrides
                .Select(o => new UserPermissionOverrideDto
                {
                    PermissionKey = o.PermissionKey,
                    Effect = o.Effect,
                    Scope = o.Scope,
                })
                .ToList(),
            EffectiveGrants = effectiveMap
                .OrderBy(kv => kv.Key)
                .Select(kv => new EffectiveGrantDto
                {
                    PermissionKey = kv.Key,
                    Scope = kv.Value,
                    SourceTag = PermissionService.ComputeSourceTag(kv.Key, roleGrants, overrideMap.GetValueOrDefault(kv.Key)),
                })
                .ToList(),
        };
    }
}
