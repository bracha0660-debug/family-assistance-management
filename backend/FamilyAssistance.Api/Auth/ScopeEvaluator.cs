using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;

namespace FamilyAssistance.Api.Auth;

public static class ScopeEvaluator
{
    public static bool CanAccessFamily(AuthorizationContext auth, Family family, string permissionKey)
    {
        if (auth.FullOrgAccess)
            return true;

        var grant = auth.GetGrant(permissionKey);
        if (grant is null)
            return false;

        if (grant.Scope == PermissionScopes.Organization)
            return true;

        return family.AssignedCoordinatorId == auth.UserId;
    }

    public static IQueryable<Family> ApplyFamilyListScope(
        IQueryable<Family> query,
        AuthorizationContext auth,
        string permissionKey)
    {
        if (auth.FullOrgAccess)
            return query;

        var grant = auth.GetGrant(permissionKey);
        if (grant is null)
            return query.Where(_ => false);

        if (grant.Scope == PermissionScopes.Organization)
            return query;

        return query.Where(f => f.AssignedCoordinatorId == auth.UserId);
    }

    public static bool CanAccessCommitteeDecision(AuthorizationContext auth, Family family, string permissionKey) =>
        CanAccessFamily(auth, family, permissionKey);

    /// <summary>Phase 14 G1 — MyRecords committee access is by decision creator.</summary>
    public static bool CanAccessCommitteeDecision(AuthorizationContext auth, CommitteeDecision decision, string permissionKey)
    {
        if (auth.FullOrgAccess)
            return true;

        var grant = auth.GetGrant(permissionKey);
        if (grant is null)
            return false;

        if (grant.Scope == PermissionScopes.Organization)
            return true;

        return decision.CreatedByUserId == auth.UserId;
    }

    public static IQueryable<CommitteeDecision> ApplyCommitteeListScope(
        IQueryable<CommitteeDecision> query,
        AuthorizationContext auth,
        string permissionKey)
    {
        if (auth.FullOrgAccess)
            return query;

        var grant = auth.GetGrant(permissionKey);
        if (grant is null)
            return query.Where(_ => false);

        if (grant.Scope == PermissionScopes.Organization)
            return query;

        // Phase 14 G1 — MyRecords for committee drafts/lists uses creator ownership.
        return query.Where(d => d.CreatedByUserId == auth.UserId);
    }

    public static IQueryable<AssistanceItem> ApplyAssistanceItemListScope(
        IQueryable<AssistanceItem> query,
        AuthorizationContext auth,
        string permissionKey)
    {
        if (auth.FullOrgAccess)
            return query;

        var grant = auth.GetGrant(permissionKey);
        if (grant is null)
            return query.Where(_ => false);

        if (grant.Scope == PermissionScopes.Organization)
            return query;

        return query.Where(i => i.CommitteeDecision!.CreatedByUserId == auth.UserId);
    }
}

