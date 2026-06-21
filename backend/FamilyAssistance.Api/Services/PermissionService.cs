using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class PermissionService(AppDbContext db)
{
    public async Task<AuthorizationContext> BuildAuthorizationContextAsync(
        CurrentUserContext current,
        CancellationToken cancellationToken = default)
    {
        if (current.Role == Roles.SuperAdmin)
        {
            return new AuthorizationContext
            {
                UserId = current.UserId,
                SystemRole = current.Role,
                ActingOrganizationId = current.ActingOrganizationId,
                Grants = [],
            };
        }

        if (current.Role == Roles.OrganizationAdministrator)
        {
            return new AuthorizationContext
            {
                UserId = current.UserId,
                SystemRole = current.Role,
                OrganizationId = current.OrganizationId,
                Grants = [],
            };
        }

        var grants = current.OrganizationRoleId is null
            ? []
            : await db.OrganizationRoleGrants
                .Where(g => g.OrganizationRoleId == current.OrganizationRoleId)
                .Select(g => new GrantContext
                {
                    PermissionKey = g.PermissionKey,
                    Scope = g.Scope,
                })
                .ToListAsync(cancellationToken);

        return new AuthorizationContext
        {
            UserId = current.UserId,
            SystemRole = current.Role,
            OrganizationId = current.OrganizationId,
            OrganizationRoleId = current.OrganizationRoleId,
            Grants = grants,
        };
    }

    public async Task<bool> HasGrantAsync(
        AuthorizationContext auth,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        if (auth.FullOrgAccess)
            return true;

        return auth.HasGrant(permissionKey);
    }

    public async Task SeedCatalogAsync(CancellationToken cancellationToken = default)
    {
        foreach (var row in PermissionCatalogSeed.Rows)
        {
            var existing = await db.PermissionCatalog.FindAsync([row.PermissionKey], cancellationToken);
            if (existing is null)
            {
                db.PermissionCatalog.Add(row);
            }
            else
            {
                existing.Category = row.Category;
                existing.DisplayNameHe = row.DisplayNameHe;
                existing.DescriptionHe = row.DescriptionHe;
                existing.SortOrder = row.SortOrder;
                existing.IsActive = true;
                existing.SupportsMyRecords = row.SupportsMyRecords;
                existing.ScopeApplies = row.ScopeApplies;
            }
        }

        var validKeys = PermissionCatalogSeed.Rows.Select(r => r.PermissionKey).ToHashSet();
        var obsolete = await db.PermissionCatalog
            .Where(p => !validKeys.Contains(p.PermissionKey))
            .ToListAsync(cancellationToken);
        foreach (var item in obsolete)
            item.IsActive = false;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedOrganizationRolesAsync(
        Guid organizationId,
        Guid? grantedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var hasRoles = await db.OrganizationRoles
            .AnyAsync(r => r.OrganizationId == organizationId, cancellationToken);
        if (hasRoles)
            return;

        var now = DateTime.UtcNow;
        foreach (var preset in RoleTemplateSeed.Presets)
        {
            var role = new OrganizationRole
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                FactoryPresetKey = preset.FactoryPresetKey,
                Name = preset.DefaultNameHe,
                Description = preset.DescriptionHe,
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.OrganizationRoles.Add(role);

            foreach (var (permissionKey, scope) in preset.Grants)
            {
                db.OrganizationRoleGrants.Add(new OrganizationRoleGrant
                {
                    Id = Guid.NewGuid(),
                    OrganizationRoleId = role.Id,
                    PermissionKey = permissionKey,
                    Scope = scope,
                    GrantedAt = now,
                    GrantedByUserId = grantedByUserId,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureAllOrganizationsHaveRolesAsync(CancellationToken cancellationToken = default)
    {
        var orgIds = await db.Organizations.Select(o => o.Id).ToListAsync(cancellationToken);
        foreach (var orgId in orgIds)
            await SeedOrganizationRolesAsync(orgId, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<GrantContext>> GetGrantsForUserAsync(
        Guid organizationId,
        Guid? organizationRoleId,
        CancellationToken cancellationToken = default)
    {
        if (organizationRoleId is null)
            return [];

        return await db.OrganizationRoleGrants
            .Where(g => g.OrganizationRoleId == organizationRoleId
                && g.OrganizationRole.OrganizationId == organizationId)
            .Select(g => new GrantContext { PermissionKey = g.PermissionKey, Scope = g.Scope })
            .ToListAsync(cancellationToken);
    }

    public static bool ValidateGrantScope(PermissionCatalog catalog, string scope)
    {
        if (!catalog.ScopeApplies)
            return scope == PermissionScopes.Organization;

        if (!catalog.SupportsMyRecords && scope != PermissionScopes.Organization)
            return false;

        if (PermissionKeys.OrganizationScopeOnly.Contains(catalog.PermissionKey)
            && scope != PermissionScopes.Organization)
            return false;

        return PermissionScopes.All.Contains(scope);
    }
}
