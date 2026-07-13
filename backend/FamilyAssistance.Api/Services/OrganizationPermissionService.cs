using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class OrganizationPermissionService(AppDbContext db)
{
    public async Task<PermissionCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await db.PermissionCatalog
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SortOrder)
            .Select(p => new PermissionCatalogItemDto
            {
                PermissionKey = p.PermissionKey,
                Category = p.Category,
                DisplayNameHe = p.DisplayNameHe,
                DescriptionHe = p.DescriptionHe,
                SortOrder = p.SortOrder,
                SupportsMyRecords = p.SupportsMyRecords,
                ScopeApplies = p.ScopeApplies,
            })
            .ToListAsync(cancellationToken);

        return new PermissionCatalogResponse { Catalog = catalog };
    }
}
