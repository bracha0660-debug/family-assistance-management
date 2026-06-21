using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class UserDtoBuilder(AppDbContext db, PermissionService permissionService)
{
    public async Task<UserDto> BuildAsync(
        User user,
        UserSession? session,
        CancellationToken cancellationToken = default)
    {
        var actingOrganizationId = session?.ActingOrganizationId;
        var organizationName = user.Organization?.Name;
        var organizationStatus = user.Organization?.Status;

        if (user.Role == Roles.SuperAdmin && actingOrganizationId is not null)
        {
            var actingOrg = await db.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == actingOrganizationId, cancellationToken);
            if (actingOrg is not null)
            {
                organizationName = actingOrg.Name;
                organizationStatus = actingOrg.Status;
            }
        }

        var current = new CurrentUserContext
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            OrganizationId = user.OrganizationId,
            OrganizationRoleId = user.OrganizationRoleId,
            ActingOrganizationId = actingOrganizationId,
            OrganizationName = organizationName,
            OrganizationStatus = organizationStatus,
            SessionId = session?.Id ?? Guid.Empty,
        };

        var auth = await permissionService.BuildAuthorizationContextAsync(current, cancellationToken);
        var grants = auth.Grants
            .Select(g => new UserGrantDto { PermissionKey = g.PermissionKey, Scope = g.Scope })
            .ToList();

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            OrganizationId = user.OrganizationId,
            OrganizationRoleId = user.OrganizationRoleId,
            ActingOrganizationId = actingOrganizationId,
            OrganizationName = organizationName,
            OrganizationStatus = organizationStatus,
            FullAccess = auth.FullOrgAccess,
            Grants = grants,
            Permissions = grants.Select(g => g.PermissionKey).ToList(),
        };
    }
}
