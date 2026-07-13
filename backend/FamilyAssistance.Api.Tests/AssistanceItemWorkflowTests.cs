using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Tests;

/// <summary>Phase 14 M82 — T1–T11 functional testability criteria.</summary>
public sealed class AssistanceItemWorkflowTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CapturingAuditService _audit;
    private readonly CommitteeDecisionService _decisions;
    private readonly AssistanceItemService _items;
    private readonly HomeWidgetComposer _home = new();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _creatorId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _assistanceTypeId = Guid.NewGuid();
    private readonly AuthorizationContext _adminAuth;
    private readonly AuthorizationContext _creatorAuth;
    private readonly AuthorizationContext _approverAuth;

    public AssistanceItemWorkflowTests()
    {
        _db = TestDbContextFactory.Create();
        _audit = new CapturingAuditService();
        _decisions = new CommitteeDecisionService(_db, _audit);
        _items = new AssistanceItemService(_db, _audit, new AssistanceItemHistoryService(_db));
        _adminAuth = new AuthorizationContext
        {
            UserId = _creatorId,
            SystemRole = Roles.OrganizationAdministrator,
            OrganizationId = _orgId,
        };
        _creatorAuth = new AuthorizationContext
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
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsApprove, Scope = PermissionScopes.Organization },
            ],
        };
        _approverAuth = new AuthorizationContext
        {
            UserId = _otherUserId,
            SystemRole = Roles.Coordinator,
            OrganizationId = _orgId,
            Grants =
            [
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsView, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsApprove, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsReject, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsExecute, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsExportBatchesCreate, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsEnterReference, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.AssistanceItemsComplete, Scope = PermissionScopes.Organization },
            ],
        };
        SeedBase();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task T1_ListApi_ReturnsStatusAvailableActionsAndContext()
    {
        var (decisionId, itemA, _) = await CreateSubmittedDecisionWithTwoItemsAsync();

        var list = await _items.ListAsync(_orgId, _approverAuth, new AssistanceItemListQuery
        {
            Status = AssistanceItemStatuses.Submitted
        });

        Assert.True(list.IsSuccess);
        Assert.NotEmpty(list.Value!.Items);
        var row = list.Value.Items.First(i => i.Id == itemA);
        Assert.Equal(AssistanceItemStatuses.Submitted, row.Status);
        Assert.Contains("approve", row.AvailableActions);
        Assert.Equal(decisionId, row.DecisionId);
        Assert.False(string.IsNullOrWhiteSpace(row.DecisionCode));
        Assert.False(string.IsNullOrWhiteSpace(row.FamilyCode));
        Assert.False(string.IsNullOrWhiteSpace(row.AssistanceTypeName));
    }

    [Fact]
    public async Task T2_SiblingIndependence_ApproveA_ReturnB()
    {
        var (_, itemA, itemB) = await CreateSubmittedDecisionWithTwoItemsAsync();

        var approve = await _items.ApproveAsync(_orgId, itemA, null, _approverAuth);
        var ret = await _items.ReturnAsync(_orgId, itemB, new StatusTransitionRequest { Reason = "חסר מסמך" }, _approverAuth);

        Assert.True(approve.IsSuccess);
        Assert.True(ret.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Approved, approve.Value!.Status);
        Assert.Equal(AssistanceItemStatuses.Returned, ret.Value!.Status);

        var a = await _db.AssistanceItems.FindAsync(itemA);
        var b = await _db.AssistanceItems.FindAsync(itemB);
        Assert.Equal(AssistanceItemStatuses.Approved, a!.Status);
        Assert.Equal(AssistanceItemStatuses.Returned, b!.Status);
    }

    [Fact]
    public async Task T3_OptionA_ApproveCreatesNoPayment_SendToExecutionCreatesPayment()
    {
        var (_, itemA, _) = await CreateSubmittedDecisionWithTwoItemsAsync();

        var approve = await _items.ApproveAsync(_orgId, itemA, null, _approverAuth);
        Assert.True(approve.IsSuccess);
        Assert.False(await _db.PaymentExecutions.AnyAsync(p => p.AssistanceItemId == itemA));

        var send = await _items.SendToExecutionAsync(_orgId, itemA, _approverAuth);
        Assert.True(send.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.WaitingForReference, send.Value!.Status);
        var payment = await _db.PaymentExecutions.SingleAsync(p => p.AssistanceItemId == itemA);
        Assert.Equal(PaymentExecutionStatuses.WaitingForReference, payment.Status);
        Assert.NotNull(send.Value.PaymentExecutionId);
    }

    [Fact]
    public async Task T4_EnterReference_TextSucceeds_EmptyFails409()
    {
        var itemId = await CreateItemAtStatusAsync(AssistanceItemStatuses.WaitingForReference);

        var empty = await _items.EnterReferenceAsync(
            _orgId, itemId, new EnterReferenceRequest { Reference = "   " }, _approverAuth);
        Assert.False(empty.IsSuccess);
        Assert.Equal(409, empty.StatusCode);

        var ok = await _items.EnterReferenceAsync(
            _orgId, itemId, new EnterReferenceRequest { Reference = "REF-123" }, _approverAuth);
        Assert.True(ok.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Paid, ok.Value!.Status);
        Assert.Equal("REF-123", ok.Value.ExecutionReference);
    }

    [Fact]
    public async Task T5_Complete_PaidToCompleted()
    {
        var itemId = await CreateItemAtStatusAsync(AssistanceItemStatuses.Paid);

        var result = await _items.CompleteAsync(_orgId, itemId, _approverAuth);
        Assert.True(result.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Completed, result.Value!.Status);
    }

    [Fact]
    public async Task T6_DraftOwnership_CreatorOnly()
    {
        var decisionId = await CreateDraftWithItemAsync(_creatorAuth);

        var otherAuth = new AuthorizationContext
        {
            UserId = _otherUserId,
            SystemRole = Roles.Coordinator,
            OrganizationId = _orgId,
            Grants =
            [
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsView, Scope = PermissionScopes.MyRecords },
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsEditDraft, Scope = PermissionScopes.MyRecords },
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsSubmit, Scope = PermissionScopes.MyRecords },
            ],
        };

        var listMine = await _decisions.ListAsync(_orgId, otherAuth, new CommitteeDecisionListQuery
        {
            Ownership = "mine",
            Status = CommitteeDecisionStatuses.Draft
        });
        Assert.True(listMine.IsSuccess);
        Assert.DoesNotContain(listMine.Value!.Decisions, d => d.Id == decisionId);

        var edit = await _decisions.UpdateDraftAsync(
            _orgId, decisionId, new UpdateCommitteeDecisionRequest { Summary = "x" }, 1, otherAuth);
        Assert.False(edit.IsSuccess);
        Assert.Equal(403, edit.StatusCode);

        var submit = await _decisions.SubmitAsync(_orgId, decisionId, 1, otherAuth);
        Assert.False(submit.IsSuccess);
        Assert.Equal(403, submit.StatusCode);
    }

    [Fact]
    public async Task T7_Submit_SetsAllItemsSubmitted_NoPayments()
    {
        var decisionId = await CreateDraftWithItemAsync(_adminAuth);
        await AddCheckItemAsync(decisionId);

        var decision = await _db.CommitteeDecisions.Include(d => d.Items).FirstAsync(d => d.Id == decisionId);
        var version = decision.Version;

        var submit = await _decisions.SubmitAsync(_orgId, decisionId, version, _adminAuth);
        Assert.True(submit.IsSuccess);

        var items = await _db.AssistanceItems.Where(i => i.CommitteeDecisionId == decisionId).ToListAsync();
        Assert.All(items, i => Assert.Equal(AssistanceItemStatuses.Submitted, i.Status));
        Assert.False(await _db.PaymentExecutions.AnyAsync(p => p.CommitteeDecisionId == decisionId));
    }

    [Fact]
    public async Task T8_DeprecatedDecisionApprove_Returns409()
    {
        var (decisionId, _, _) = await CreateSubmittedDecisionWithTwoItemsAsync();
        var decision = await _db.CommitteeDecisions.FindAsync(decisionId);

        var result = await _decisions.ApproveAsync(_orgId, decisionId, null, decision!.Version, _approverAuth);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("DEPRECATED_ENDPOINT", result.Code);
    }

    [Fact]
    public async Task T9_Dashboard_ItemKpisAndListView()
    {
        var (_, itemA, itemB) = await CreateSubmittedDecisionWithTwoItemsAsync();
        await _items.ApproveAsync(_orgId, itemA, null, _approverAuth);
        await _items.ReturnAsync(_orgId, itemB, new StatusTransitionRequest { Reason = "תיקון" }, _approverAuth);

        var decisions = await _db.CommitteeDecisions
            .Include(d => d.Family)
            .Include(d => d.Items)
                .ThenInclude(i => i.PaymentExecution)
            .Where(d => d.OrganizationId == _orgId)
            .ToListAsync();

        var home = _home.Compose(_approverAuth, decisions, [], []);
        var kpiWidget = home.Widgets.First(w => w.Type == HomeWidgetTypes.KpiCards);
        var cards = kpiWidget.Data!.Value.Deserialize<HomeKpiCardsDataDto>(
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        var awaiting = cards.Cards.First(c => c.KpiKey == "awaiting_approval");
        Assert.Equal(0, awaiting.Count);
        Assert.Equal(HomeWidgetComposer.ListViewAssistanceItems, awaiting.NavigationTarget.ListView);
        Assert.Equal("decisions", awaiting.NavigationTarget.TargetTab);

        var returned = cards.Cards.First(c => c.KpiKey == "returned_for_revision");
        Assert.Equal(1, returned.Count);
        Assert.Equal(HomeWidgetComposer.ListViewAssistanceItems, returned.NavigationTarget.ListView);
    }

    [Fact]
    public async Task T10_SelfApproval_CreatorWithApproveGrant()
    {
        var (_, itemA, _) = await CreateSubmittedDecisionWithTwoItemsAsync();

        var result = await _items.ApproveAsync(_orgId, itemA, null, _creatorAuth);
        Assert.True(result.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Approved, result.Value!.Status);
    }

    [Fact]
    public async Task T11_DraftCrud_StillWorks()
    {
        var decisionId = await CreateDraftWithItemAsync(_adminAuth);
        var add = await AddCheckItemAsync(decisionId);
        Assert.True(add.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Draft, (await _db.AssistanceItems.FindAsync(add.Value!.Item.Id))!.Status);

        var get = await _decisions.GetAsync(_orgId, decisionId, _adminAuth);
        Assert.True(get.IsSuccess);
        Assert.Contains(get.Value!.Items, i => i.Status == AssistanceItemStatuses.Draft);
    }

    private async Task<(Guid DecisionId, Guid ItemA, Guid ItemB)> CreateSubmittedDecisionWithTwoItemsAsync()
    {
        var decisionId = await CreateDraftWithItemAsync(_adminAuth);
        var a = await AddCheckItemAsync(decisionId);
        var b = await AddCheckItemAsync(decisionId);
        Assert.True(a.IsSuccess);
        Assert.True(b.IsSuccess);

        var decision = await _db.CommitteeDecisions.FindAsync(decisionId);
        var submit = await _decisions.SubmitAsync(_orgId, decisionId, decision!.Version, _adminAuth);
        Assert.True(submit.IsSuccess);
        return (decisionId, a.Value!.Item.Id, b.Value!.Item.Id);
    }

    private async Task<Guid> CreateDraftWithItemAsync(AuthorizationContext auth)
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
            CreatedByUserId = auth.UserId,
            TotalAmount = 0,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await _db.SaveChangesAsync();
        return decisionId;
    }

    private async Task<ServiceResult<(AssistanceItemDto Item, int DecisionVersion)>> AddCheckItemAsync(Guid decisionId)
    {
        var decision = await _db.CommitteeDecisions.FindAsync(decisionId);
        return await _decisions.AddItemAsync(
            _orgId,
            decisionId,
            new CreateAssistanceItemRequest
            {
                AssistanceTypeId = _assistanceTypeId,
                Amount = 100,
                PaymentTarget = PaymentTargets.Family,
                PaymentMethod = PaymentMethods.Check,
                PayeeName = "כהן",
            },
            decision!.Version,
            _adminAuth);
    }

    private async Task<Guid> CreateItemAtStatusAsync(string status)
    {
        var (_, itemA, _) = await CreateSubmittedDecisionWithTwoItemsAsync();
        if (status == AssistanceItemStatuses.Submitted)
            return itemA;

        Assert.True((await _items.ApproveAsync(_orgId, itemA, null, _approverAuth)).IsSuccess);
        if (status == AssistanceItemStatuses.Approved)
            return itemA;

        Assert.True((await _items.SendToExecutionAsync(_orgId, itemA, _approverAuth)).IsSuccess);
        if (status == AssistanceItemStatuses.WaitingForReference)
            return itemA;

        Assert.True((await _items.EnterReferenceAsync(
            _orgId, itemA, new EnterReferenceRequest { Reference = "R1" }, _approverAuth)).IsSuccess);
        if (status == AssistanceItemStatuses.Paid)
            return itemA;

        throw new InvalidOperationException($"Unsupported target status {status}");
    }

    private void SeedBase()
    {
        var now = DateTime.UtcNow;
        _db.Organizations.Add(new Organization
        {
            Id = _orgId,
            Name = "Org",
            Code = "ORG14",
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        _db.Users.AddRange(
            new User
            {
                Id = _creatorId,
                OrganizationId = _orgId,
                Username = "creator",
                PasswordHash = "hash",
                FullName = "Creator",
                Role = Roles.OrganizationAdministrator,
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new User
            {
                Id = _otherUserId,
                OrganizationId = _orgId,
                Username = "other",
                PasswordHash = "hash",
                FullName = "Other",
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
            FamilyCode = "F-000014",
            AccountingCode = 1014,
            AccountingCoordinatorId = _creatorId,
            AssignedCoordinatorId = _creatorId,
            FamilyLastName = "לוי",
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        _db.AssistanceTypes.Add(new AssistanceType
        {
            Id = _assistanceTypeId,
            OrganizationId = _orgId,
            TypeCode = "HELP14",
            Name = "סיוע",
            Frequency = "monthly",
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
