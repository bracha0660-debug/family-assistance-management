using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Tests;

/// <summary>Phase 16.3 Stage 1 — SuperAdmin-in-org ownership bypass (not FullOrgAccess).</summary>
public sealed class Phase163SuperAdminOwnershipTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CapturingAuditService _audit;
    private readonly CommitteeDecisionService _decisions;
    private readonly AssistanceItemService _items;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _otherOrgId = Guid.NewGuid();
    private readonly Guid _creatorId = Guid.NewGuid();
    private readonly Guid _superAdminId = Guid.NewGuid();
    private readonly Guid _orgAdminId = Guid.NewGuid();
    private readonly Guid _ordinaryId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _assistanceTypeId = Guid.NewGuid();

    public Phase163SuperAdminOwnershipTests()
    {
        _db = TestDbContextFactory.Create();
        _audit = new CapturingAuditService();
        _decisions = new CommitteeDecisionService(_db, _audit);
        _items = new AssistanceItemService(_db, _audit, new AssistanceItemHistoryService(_db));
        SeedBase();
    }

    public void Dispose() => _db.Dispose();

    private AuthorizationContext SuperAdminInOrg => new()
    {
        UserId = _superAdminId,
        SystemRole = Roles.SuperAdmin,
        OrganizationId = null,
        ActingOrganizationId = _orgId,
    };

    private AuthorizationContext SuperAdminNoOrg => new()
    {
        UserId = _superAdminId,
        SystemRole = Roles.SuperAdmin,
        OrganizationId = null,
        ActingOrganizationId = null,
    };

    private AuthorizationContext OrgAdminAuth => new()
    {
        UserId = _orgAdminId,
        SystemRole = Roles.OrganizationAdministrator,
        OrganizationId = _orgId,
    };

    private AuthorizationContext CreatorAuth => new()
    {
        UserId = _creatorId,
        SystemRole = Roles.Coordinator,
        OrganizationId = _orgId,
        Grants =
        [
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsView, Scope = PermissionScopes.MyRecords },
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsCreate, Scope = PermissionScopes.Organization },
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsEditDraft, Scope = PermissionScopes.MyRecords },
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsSubmit, Scope = PermissionScopes.MyRecords },
            new GrantContext { PermissionKey = PermissionKeys.AssistanceItemsCreate, Scope = PermissionScopes.Organization },
            new GrantContext { PermissionKey = PermissionKeys.AssistanceItemsEdit, Scope = PermissionScopes.MyRecords },
        ],
    };

    private AuthorizationContext OrdinaryOtherAuth => new()
    {
        UserId = _ordinaryId,
        SystemRole = Roles.Coordinator,
        OrganizationId = _orgId,
        Grants =
        [
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsView, Scope = PermissionScopes.MyRecords },
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsEditDraft, Scope = PermissionScopes.MyRecords },
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsSubmit, Scope = PermissionScopes.MyRecords },
            new GrantContext { PermissionKey = PermissionKeys.AssistanceItemsEdit, Scope = PermissionScopes.MyRecords },
        ],
    };

    private AuthorizationContext ApproverAuth => new()
    {
        UserId = _ordinaryId,
        SystemRole = Roles.Coordinator,
        OrganizationId = _orgId,
        Grants =
        [
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsView, Scope = PermissionScopes.Organization },
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsApprove, Scope = PermissionScopes.Organization },
            new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsReject, Scope = PermissionScopes.Organization },
        ],
    };

    [Fact]
    public void Auth_IsSuperAdminInOrganization_RequiresActingOrg()
    {
        Assert.True(SuperAdminInOrg.IsSuperAdminInOrganization);
        Assert.False(SuperAdminNoOrg.IsSuperAdminInOrganization);
        Assert.False(OrgAdminAuth.IsSuperAdminInOrganization);
        Assert.True(OrgAdminAuth.FullOrgAccess);
        Assert.True(SuperAdminInOrg.FullOrgAccess);
        Assert.False(ReferenceEquals(
            typeof(AuthorizationContext).GetProperty(nameof(AuthorizationContext.IsSuperAdminInOrganization)),
            typeof(AuthorizationContext).GetProperty(nameof(AuthorizationContext.FullOrgAccess))));
    }

    [Fact]
    public async Task SuperAdmin_WithoutActingOrg_OrgAccessRemainsForbidden()
    {
        var (decisionId, _) = await CreateCreatorDraftWithItemAsync();

        Assert.Null(SuperAdminNoOrg.EffectiveOrganizationId);
        Assert.False(SuperAdminNoOrg.IsSuperAdminInOrganization);
        Assert.False(PermissionService.HasWorkflowGrant(
            SuperAdminNoOrg, PermissionKeys.CommitteeDecisionsEditDraft));

        var get = await _decisions.GetAsync(_orgId, decisionId, SuperAdminNoOrg);
        Assert.False(get.IsSuccess);
        Assert.Equal(403, get.StatusCode);

        var update = await _decisions.UpdateDraftAsync(
            _orgId, decisionId, new UpdateCommitteeDecisionRequest { Summary = "x" }, 1, SuperAdminNoOrg);
        Assert.False(update.IsSuccess);
        Assert.Equal(403, update.StatusCode);

        var list = await _decisions.ListAsync(_orgId, SuperAdminNoOrg, new CommitteeDecisionListQuery
        {
            Status = CommitteeDecisionStatuses.Draft,
        });
        Assert.True(list.IsSuccess);
        Assert.Empty(list.Value!.Decisions);
    }

    [Fact]
    public async Task SuperAdmin_AfterEnter_SeesOtherCreatorsDraft_OrgWideList()
    {
        var (decisionId, _) = await CreateCreatorDraftWithItemAsync();

        var orgWide = await _decisions.ListAsync(_orgId, SuperAdminInOrg, new CommitteeDecisionListQuery
        {
            Status = CommitteeDecisionStatuses.Draft,
        });
        Assert.True(orgWide.IsSuccess);
        Assert.Contains(orgWide.Value!.Decisions, d => d.Id == decisionId);
    }

    [Fact]
    public async Task SuperAdmin_ExplicitOwnershipMine_StillExcludesOthers()
    {
        var (decisionId, _) = await CreateCreatorDraftWithItemAsync();

        var mine = await _decisions.ListAsync(_orgId, SuperAdminInOrg, new CommitteeDecisionListQuery
        {
            Status = CommitteeDecisionStatuses.Draft,
            Ownership = "mine",
        });
        Assert.True(mine.IsSuccess);
        Assert.DoesNotContain(mine.Value!.Decisions, d => d.Id == decisionId);
    }

    [Fact]
    public async Task SuperAdmin_AvailableActions_IncludesEditAndSubmit_OnOtherDraft()
    {
        var (decisionId, _) = await CreateCreatorDraftWithItemAsync();

        var get = await _decisions.GetAsync(_orgId, decisionId, SuperAdminInOrg);
        Assert.True(get.IsSuccess);
        Assert.False(get.Value!.IsOwnedByCurrentUser);
        Assert.Contains("edit", get.Value.AvailableActions);
        Assert.Contains("submit", get.Value.AvailableActions);
    }

    [Fact]
    public async Task SuperAdmin_Mutations_Succeed_OnOtherUsersDraft()
    {
        var (decisionId, itemId) = await CreateCreatorDraftWithItemAsync();
        var decision = await _db.CommitteeDecisions.FindAsync(decisionId);
        var version = decision!.Version;

        var update = await _decisions.UpdateDraftAsync(
            _orgId,
            decisionId,
            new UpdateCommitteeDecisionRequest { Summary = "SA edit" },
            version,
            SuperAdminInOrg);
        Assert.True(update.IsSuccess);
        Assert.Equal("SA edit", update.Value!.Summary);
        Assert.Contains(_audit.StagedEntries, e =>
            e.ActorUserId == _superAdminId
            && e.OrganizationId == _orgId
            && e.EntityId == decisionId);

        version = update.Value.Version;
        var add = await _decisions.AddItemAsync(
            _orgId,
            decisionId,
            new CreateAssistanceItemRequest
            {
                AssistanceTypeId = _assistanceTypeId,
                Amount = 50,
                PaymentTarget = PaymentTargets.Family,
                PaymentMethod = PaymentMethods.Check,
                PayeeName = "SA Payee",
                Description = "SA item",
            },
            version,
            SuperAdminInOrg);
        Assert.True(add.IsSuccess);

        var item = await _db.AssistanceItems.FindAsync(itemId);
        var itemUpdate = await _decisions.UpdateItemAsync(
            _orgId,
            decisionId,
            itemId,
            new UpdateAssistanceItemRequest { Description = "updated by SA", Amount = 120 },
            item!.Version,
            SuperAdminInOrg);
        Assert.True(itemUpdate.IsSuccess);
        Assert.Equal("updated by SA", itemUpdate.Value!.Description);

        decision = await _db.CommitteeDecisions.Include(d => d.Items).FirstAsync(d => d.Id == decisionId);
        var submit = await _decisions.SubmitAsync(_orgId, decisionId, decision.Version, SuperAdminInOrg);
        Assert.True(submit.IsSuccess);
        Assert.Equal(CommitteeDecisionStatuses.Submitted, submit.Value!.Status);
    }

    [Fact]
    public async Task SuperAdmin_Resubmit_Succeeds_OnOtherUsersReturnedItem()
    {
        var (decisionId, itemId) = await CreateCreatorDraftWithItemAsync();
        var decision = await _db.CommitteeDecisions.FindAsync(decisionId);
        Assert.True((await _decisions.SubmitAsync(_orgId, decisionId, decision!.Version, SuperAdminInOrg)).IsSuccess);

        var ret = await _items.ReturnAsync(
            _orgId, itemId, new StatusTransitionRequest { Reason = "needs fix" }, ApproverAuth);
        Assert.True(ret.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Returned, ret.Value!.Status);

        var itemGet = await _items.ListAsync(_orgId, SuperAdminInOrg, new AssistanceItemListQuery
        {
            Status = AssistanceItemStatuses.Returned,
        });
        Assert.True(itemGet.IsSuccess);
        var row = itemGet.Value!.Items.First(i => i.Id == itemId);
        Assert.Contains("resubmit", row.AvailableActions);
        Assert.Contains("edit", row.AvailableActions);

        var resubmit = await _items.ResubmitAsync(_orgId, itemId, SuperAdminInOrg);
        Assert.True(resubmit.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Submitted, resubmit.Value!.Status);
    }

    [Fact]
    public async Task SuperAdmin_StatusInvalid_StillRejected()
    {
        var (decisionId, itemId) = await CreateCreatorDraftWithItemAsync();

        var resubmitDraft = await _items.ResubmitAsync(_orgId, itemId, SuperAdminInOrg);
        Assert.False(resubmitDraft.IsSuccess);
        Assert.Equal(409, resubmitDraft.StatusCode);
        Assert.Equal("INVALID_STATUS", resubmitDraft.Code);

        var decision = await _db.CommitteeDecisions.FindAsync(decisionId);
        Assert.True((await _decisions.SubmitAsync(_orgId, decisionId, decision!.Version, SuperAdminInOrg)).IsSuccess);

        var updateSubmitted = await _decisions.UpdateDraftAsync(
            _orgId,
            decisionId,
            new UpdateCommitteeDecisionRequest { Summary = "too late" },
            (await _db.CommitteeDecisions.FindAsync(decisionId))!.Version,
            SuperAdminInOrg);
        Assert.False(updateSubmitted.IsSuccess);
        Assert.Equal(409, updateSubmitted.StatusCode);
    }

    [Fact]
    public async Task OrgAdmin_Ownership_Unchanged_CannotMutateOthersDraft()
    {
        var (decisionId, _) = await CreateCreatorDraftWithItemAsync();

        Assert.False(OrgAdminAuth.IsSuperAdminInOrganization);
        Assert.True(OrgAdminAuth.FullOrgAccess);

        var get = await _decisions.GetAsync(_orgId, decisionId, OrgAdminAuth);
        Assert.True(get.IsSuccess);
        // OrgAdmin has FullOrgAccess for scope, but HasWorkflowGrant is false — no edit/submit actions.
        Assert.DoesNotContain("edit", get.Value!.AvailableActions);
        Assert.DoesNotContain("submit", get.Value.AvailableActions);

        var update = await _decisions.UpdateDraftAsync(
            _orgId, decisionId, new UpdateCommitteeDecisionRequest { Summary = "orgadmin" }, 1, OrgAdminAuth);
        Assert.False(update.IsSuccess);
        Assert.Equal(403, update.StatusCode);
    }

    [Fact]
    public async Task OrdinaryUser_Ownership_StillProtected()
    {
        var (decisionId, _) = await CreateCreatorDraftWithItemAsync();

        var listMine = await _decisions.ListAsync(_orgId, OrdinaryOtherAuth, new CommitteeDecisionListQuery
        {
            Status = CommitteeDecisionStatuses.Draft,
            Ownership = "mine",
        });
        Assert.DoesNotContain(listMine.Value!.Decisions, d => d.Id == decisionId);

        var update = await _decisions.UpdateDraftAsync(
            _orgId, decisionId, new UpdateCommitteeDecisionRequest { Summary = "steal" }, 1, OrdinaryOtherAuth);
        Assert.False(update.IsSuccess);
        Assert.Equal(403, update.StatusCode);
    }

    [Fact]
    public async Task CrossOrganization_Access_Blocked()
    {
        var (decisionId, itemId) = await CreateCreatorDraftWithItemAsync();
        var saOtherOrg = new AuthorizationContext
        {
            UserId = _superAdminId,
            SystemRole = Roles.SuperAdmin,
            ActingOrganizationId = _otherOrgId,
        };

        var get = await _decisions.GetAsync(_otherOrgId, decisionId, saOtherOrg);
        Assert.False(get.IsSuccess);
        Assert.Equal(404, get.StatusCode);

        var update = await _decisions.UpdateDraftAsync(
            _otherOrgId, decisionId, new UpdateCommitteeDecisionRequest { Summary = "x" }, 1, saOtherOrg);
        Assert.False(update.IsSuccess);
        Assert.Equal(404, update.StatusCode);

        var resubmit = await _items.ResubmitAsync(_otherOrgId, itemId, saOtherOrg);
        Assert.False(resubmit.IsSuccess);
        Assert.Equal(404, resubmit.StatusCode);
    }

    private async Task<(Guid DecisionId, Guid ItemId)> CreateCreatorDraftWithItemAsync()
    {
        var now = DateTime.UtcNow;
        var decisionId = Guid.NewGuid();
        _db.CommitteeDecisions.Add(new CommitteeDecision
        {
            Id = decisionId,
            OrganizationId = _orgId,
            DecisionCode = $"D-{Guid.NewGuid():N}"[..10],
            FamilyId = _familyId,
            MeetingDate = DateOnly.FromDateTime(now),
            Status = CommitteeDecisionStatuses.Draft,
            CreatedByUserId = _creatorId,
            Summary = "creator draft",
            TotalAmount = 0,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await _db.SaveChangesAsync();

        var add = await _decisions.AddItemAsync(
            _orgId,
            decisionId,
            new CreateAssistanceItemRequest
            {
                AssistanceTypeId = _assistanceTypeId,
                Amount = 100,
                PaymentTarget = PaymentTargets.Family,
                PaymentMethod = PaymentMethods.Check,
                PayeeName = "כהן",
                Description = "item",
            },
            1,
            CreatorAuth);
        Assert.True(add.IsSuccess);
        return (decisionId, add.Value!.Item.Id);
    }

    private void SeedBase()
    {
        var now = DateTime.UtcNow;
        _db.Organizations.AddRange(
            new Organization
            {
                Id = _orgId,
                Name = "Org A",
                Code = "ORGA163",
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Organization
            {
                Id = _otherOrgId,
                Name = "Org B",
                Code = "ORGB163",
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            });
        _db.Users.AddRange(
            new User
            {
                Id = _superAdminId,
                OrganizationId = null,
                Username = "superadmin163",
                PasswordHash = "hash",
                FullName = "Super",
                Role = Roles.SuperAdmin,
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new User
            {
                Id = _creatorId,
                OrganizationId = _orgId,
                Username = "creator163",
                PasswordHash = "hash",
                FullName = "Creator",
                Role = Roles.Coordinator,
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new User
            {
                Id = _orgAdminId,
                OrganizationId = _orgId,
                Username = "orgadmin163",
                PasswordHash = "hash",
                FullName = "OrgAdmin",
                Role = Roles.OrganizationAdministrator,
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new User
            {
                Id = _ordinaryId,
                OrganizationId = _orgId,
                Username = "ordinary163",
                PasswordHash = "hash",
                FullName = "Ordinary",
                Role = Roles.Coordinator,
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            });
        _db.Families.Add(new Family
        {
            Id = _familyId,
            OrganizationId = _orgId,
            FamilyCode = "F-000163",
            AccountingCode = 1163,
            AccountingCoordinatorId = _creatorId,
            AssignedCoordinatorId = _creatorId,
            FamilyLastName = "כהן",
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        _db.AssistanceTypes.Add(new AssistanceType
        {
            Id = _assistanceTypeId,
            OrganizationId = _orgId,
            TypeCode = "H163",
            Name = "סיוע",
            Frequency = "one_time",
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        _db.SaveChanges();
    }

    private sealed class CapturingAuditService : IAuditService
    {
        public List<AuditEntry> StagedEntries { get; } = [];
        public void Stage(AuditEntry entry) => StagedEntries.Add(entry);
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
