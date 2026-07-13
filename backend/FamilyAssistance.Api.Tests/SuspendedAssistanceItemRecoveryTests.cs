using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Tests;

/// <summary>Phase 16 Suspended AssistanceItem Recovery — SR5 contract tests.</summary>
public sealed class SuspendedAssistanceItemRecoveryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CapturingAuditService _audit;
    private readonly CommitteeDecisionService _decisions;
    private readonly AssistanceItemService _items;
    private readonly AssistanceItemHistoryService _history;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _creatorId = Guid.NewGuid();
    private readonly Guid _approverId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _assistanceTypeId = Guid.NewGuid();
    private readonly AuthorizationContext _adminAuth;
    private readonly AuthorizationContext _approverAuth;
    private readonly AuthorizationContext _viewOnlyAuth;

    public SuspendedAssistanceItemRecoveryTests()
    {
        _db = TestDbContextFactory.Create();
        _audit = new CapturingAuditService();
        _history = new AssistanceItemHistoryService(_db);
        _decisions = new CommitteeDecisionService(_db, _audit);
        _items = new AssistanceItemService(_db, _audit, _history);
        _adminAuth = new AuthorizationContext
        {
            UserId = _creatorId,
            SystemRole = Roles.OrganizationAdministrator,
            OrganizationId = _orgId,
        };
        _approverAuth = new AuthorizationContext
        {
            UserId = _approverId,
            SystemRole = Roles.Coordinator,
            OrganizationId = _orgId,
            Grants =
            [
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsView, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsApprove, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsReject, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.AssistanceItemsViewHistory, Scope = PermissionScopes.Organization },
            ],
        };
        _viewOnlyAuth = new AuthorizationContext
        {
            UserId = Guid.NewGuid(),
            SystemRole = Roles.Coordinator,
            OrganizationId = _orgId,
            Grants =
            [
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsView, Scope = PermissionScopes.Organization },
            ],
        };
        SeedBase();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SR5_1_Suspended_WithApproveGrant_ContainsApprove_NotRestore()
    {
        var itemId = await CreateSuspendedItemAsync();
        var item = await LoadItem(itemId);
        var actions = WorkflowHelpers.AvailableAssistanceItemActions(item, item.CommitteeDecision!, _approverAuth);

        Assert.Contains("approve", actions);
        Assert.DoesNotContain("restore", actions);
        Assert.DoesNotContain("unsuspend", actions);
        Assert.DoesNotContain("resume", actions);
    }

    [Fact]
    public async Task SR5_2_Suspended_WithRejectGrant_ContainsRejectAndReturn()
    {
        var itemId = await CreateSuspendedItemAsync();
        var item = await LoadItem(itemId);
        var actions = WorkflowHelpers.AvailableAssistanceItemActions(item, item.CommitteeDecision!, _approverAuth);

        Assert.Contains("reject", actions);
        Assert.Contains("return", actions);
    }

    [Fact]
    public async Task SR5_3_Suspended_Unauthorized_ActionsAbsent_AndApisForbidden()
    {
        var itemId = await CreateSuspendedItemAsync();
        var item = await LoadItem(itemId);
        var actions = WorkflowHelpers.AvailableAssistanceItemActions(item, item.CommitteeDecision!, _viewOnlyAuth);

        Assert.DoesNotContain("approve", actions);
        Assert.DoesNotContain("reject", actions);
        Assert.DoesNotContain("return", actions);

        var approve = await _items.ApproveAsync(_orgId, itemId, null, _viewOnlyAuth);
        Assert.False(approve.IsSuccess);
        Assert.Equal(403, approve.StatusCode);

        var reject = await _items.RejectAsync(
            _orgId, itemId, new StatusTransitionRequest { Reason = "אין אישור" }, _viewOnlyAuth);
        Assert.False(reject.IsSuccess);
        Assert.Equal(403, reject.StatusCode);

        var ret = await _items.ReturnAsync(
            _orgId, itemId, new StatusTransitionRequest { Reason = "להחזיר" }, _viewOnlyAuth);
        Assert.False(ret.IsSuccess);
        Assert.Equal(403, ret.StatusCode);
    }

    [Fact]
    public async Task SR5_4_ApproveFromSuspended_StatusHistoryAndActions()
    {
        var itemId = await CreateSuspendedItemAsync();
        var before = DateTime.UtcNow.AddSeconds(-2);

        var result = await _items.ApproveAsync(_orgId, itemId, null, _approverAuth);
        Assert.True(result.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Approved, result.Value!.Status);
        Assert.DoesNotContain("approve", result.Value.AvailableActions);
        Assert.Contains("suspend", result.Value.AvailableActions);

        var history = await _db.AssistanceItemHistoryEvents
            .Include(e => e.FieldChanges)
            .Where(e => e.AssistanceItemId == itemId && e.EventType == AssistanceItemHistoryEventTypes.Approved)
            .OrderByDescending(e => e.OccurredAt)
            .FirstAsync();
        Assert.True(history.OccurredAt >= before);
        Assert.Equal(_approverId, history.ActorUserId);
        var statusChange = Assert.Single(history.FieldChanges, c => c.FieldKey == AssistanceItemEditableFields.Status);
        Assert.Equal(AssistanceItemStatuses.Suspended, statusChange.PreviousValue);
        Assert.Equal(AssistanceItemStatuses.Approved, statusChange.NewValue);
    }

    [Fact]
    public async Task SR5_5_RejectFromSuspended_RequiresReason_AndHistory()
    {
        var itemId = await CreateSuspendedItemAsync();

        var missingReason = await _items.RejectAsync(
            _orgId, itemId, new StatusTransitionRequest { Reason = "x" }, _approverAuth);
        Assert.False(missingReason.IsSuccess);
        Assert.Equal(400, missingReason.StatusCode);

        var result = await _items.RejectAsync(
            _orgId, itemId, new StatusTransitionRequest { Reason = "נדחה אחרי השהיה" }, _approverAuth);
        Assert.True(result.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Rejected, result.Value!.Status);

        var history = await _db.AssistanceItemHistoryEvents
            .Include(e => e.FieldChanges)
            .Where(e => e.AssistanceItemId == itemId && e.EventType == AssistanceItemHistoryEventTypes.Rejected)
            .OrderByDescending(e => e.OccurredAt)
            .FirstAsync();
        Assert.Equal("נדחה אחרי השהיה", history.Reason);
        var statusChange = Assert.Single(history.FieldChanges, c => c.FieldKey == AssistanceItemEditableFields.Status);
        Assert.Equal(AssistanceItemStatuses.Suspended, statusChange.PreviousValue);
        Assert.Equal(AssistanceItemStatuses.Rejected, statusChange.NewValue);
    }

    [Fact]
    public async Task SR5_6_ReturnFromSuspended_RequiresReason_AndHistory()
    {
        var itemId = await CreateSuspendedItemAsync();

        var result = await _items.ReturnAsync(
            _orgId, itemId, new StatusTransitionRequest { Reason = "להשלים מסמכים" }, _approverAuth);
        Assert.True(result.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Returned, result.Value!.Status);
        Assert.DoesNotContain("approve", result.Value.AvailableActions);
        Assert.DoesNotContain("reject", result.Value.AvailableActions);

        var history = await _db.AssistanceItemHistoryEvents
            .Include(e => e.FieldChanges)
            .Where(e => e.AssistanceItemId == itemId && e.EventType == AssistanceItemHistoryEventTypes.Returned)
            .OrderByDescending(e => e.OccurredAt)
            .FirstAsync();
        Assert.Equal("להשלים מסמכים", history.Reason);
        var statusChange = Assert.Single(history.FieldChanges, c => c.FieldKey == AssistanceItemEditableFields.Status);
        Assert.Equal(AssistanceItemStatuses.Suspended, statusChange.PreviousValue);
        Assert.Equal(AssistanceItemStatuses.Returned, statusChange.NewValue);
    }

    [Fact]
    public async Task SR5_7_NoRestoreUnsuspendResume_EndpointsOrActions()
    {
        var itemId = await CreateSuspendedItemAsync();
        var item = await LoadItem(itemId);
        var actions = WorkflowHelpers.AvailableAssistanceItemActions(item, item.CommitteeDecision!, _approverAuth);

        Assert.DoesNotContain(actions, a => a is "restore" or "unsuspend" or "resume");

        // Illegal sources remain rejected (e.g. approve from paid).
        var paidId = await CreateItemAtPaidAsync();
        var illegal = await _items.ApproveAsync(_orgId, paidId, null, _approverAuth);
        Assert.False(illegal.IsSuccess);
        Assert.Equal(409, illegal.StatusCode);
        Assert.Equal("INVALID_STATUS", illegal.Code);
    }

    [Fact]
    public async Task SR5_8_AfterApprove_AvailableActionsMatchApprovedNotSuspended()
    {
        var itemId = await CreateSuspendedItemAsync();
        var result = await _items.ApproveAsync(_orgId, itemId, null, _approverAuth);
        Assert.True(result.IsSuccess);

        Assert.Equal(AssistanceItemStatuses.Approved, result.Value!.Status);
        Assert.DoesNotContain("reject", result.Value.AvailableActions);
        Assert.DoesNotContain("return", result.Value.AvailableActions);
        Assert.Contains("suspend", result.Value.AvailableActions);
    }

    private async Task<Guid> CreateSuspendedItemAsync()
    {
        var (_, itemA, _) = await CreateSubmittedDecisionWithTwoItemsAsync();
        var suspend = await _items.SuspendAsync(
            _orgId, itemA, new StatusTransitionRequest { Reason = "השהיה לבדיקה" }, _approverAuth);
        Assert.True(suspend.IsSuccess);
        Assert.Equal(AssistanceItemStatuses.Suspended, suspend.Value!.Status);
        return itemA;
    }

    private async Task<Guid> CreateItemAtPaidAsync()
    {
        var (_, itemA, _) = await CreateSubmittedDecisionWithTwoItemsAsync();
        Assert.True((await _items.ApproveAsync(_orgId, itemA, null, _approverAuth)).IsSuccess);

        // SendToExecution needs export create grant — elevate temporarily via admin path:
        var exportAuth = new AuthorizationContext
        {
            UserId = _approverId,
            SystemRole = Roles.Coordinator,
            OrganizationId = _orgId,
            Grants =
            [
                new GrantContext { PermissionKey = PermissionKeys.PaymentsExportBatchesCreate, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsEnterReference, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.CommitteeDecisionsApprove, Scope = PermissionScopes.Organization },
            ],
        };
        Assert.True((await _items.SendToExecutionAsync(_orgId, itemA, exportAuth)).IsSuccess);
        Assert.True((await _items.EnterReferenceAsync(
            _orgId, itemA, new EnterReferenceRequest { Reference = "REF-SR" }, exportAuth)).IsSuccess);
        return itemA;
    }

    private async Task<AssistanceItem> LoadItem(Guid itemId) =>
        await _db.AssistanceItems
            .Include(i => i.CommitteeDecision)!
                .ThenInclude(d => d!.Items)
            .FirstAsync(i => i.Id == itemId);

    private async Task<(Guid DecisionId, Guid ItemA, Guid ItemB)> CreateSubmittedDecisionWithTwoItemsAsync()
    {
        var decisionId = await CreateDraftAsync();
        var a = await AddCheckItemAsync(decisionId);
        var b = await AddCheckItemAsync(decisionId);
        Assert.True(a.IsSuccess);
        Assert.True(b.IsSuccess);
        var decision = await _db.CommitteeDecisions.FindAsync(decisionId);
        var submit = await _decisions.SubmitAsync(_orgId, decisionId, decision!.Version, _adminAuth);
        Assert.True(submit.IsSuccess);
        return (decisionId, a.Value!.Item.Id, b.Value!.Item.Id);
    }

    private async Task<Guid> CreateDraftAsync()
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

    private void SeedBase()
    {
        var now = DateTime.UtcNow;
        _db.Organizations.Add(new Organization
        {
            Id = _orgId,
            Name = "Org",
            Code = "ORGSR",
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
                Username = "creator-sr",
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
                Id = _approverId,
                OrganizationId = _orgId,
                Username = "approver-sr",
                PasswordHash = "hash",
                FullName = "Approver",
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
            FamilyCode = "F-000SR1",
            AccountingCode = 1901,
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
            TypeCode = "HELPSR",
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
