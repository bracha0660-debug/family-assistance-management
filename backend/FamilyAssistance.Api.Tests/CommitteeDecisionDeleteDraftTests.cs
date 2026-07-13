using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Tests;

public sealed class CommitteeDecisionDeleteDraftTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CapturingAuditService _audit;
    private readonly CommitteeDecisionService _service;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _outsiderId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _assistanceTypeId = Guid.NewGuid();
    private readonly Guid _decisionId = Guid.NewGuid();
    private readonly AuthorizationContext _auth;

    public CommitteeDecisionDeleteDraftTests()
    {
        _db = TestDbContextFactory.Create();
        _audit = new CapturingAuditService();
        _service = new CommitteeDecisionService(_db, _audit);
        _auth = new AuthorizationContext
        {
            UserId = _actorId,
            SystemRole = Roles.OrganizationAdministrator,
            OrganizationId = _orgId,
        };
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task DeleteDraft_WithItems_RemovesDecisionItemsAndStagesAudit()
    {
        _db.AssistanceItems.Add(new AssistanceItem
        {
            Id = Guid.NewGuid(),
            CommitteeDecisionId = _decisionId,
            LineNumber = 1,
            AssistanceTypeId = _assistanceTypeId,
            Amount = 100,
            PaymentTarget = PaymentTargets.Family,
            PaymentMethod = PaymentMethods.Check,
            PayeeName = "כהן",
            Status = AssistanceItemStatuses.Draft,
            ExecutionStatus = PaymentExecutionStatuses.AwaitingPayment,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _service.DeleteDraftAsync(_orgId, _decisionId, 1, _auth);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.False(await _db.CommitteeDecisions.AnyAsync(d => d.Id == _decisionId));
        Assert.False(await _db.AssistanceItems.AnyAsync(i => i.CommitteeDecisionId == _decisionId));
        Assert.Single(_audit.StagedEntries);
        Assert.Equal(BusinessEventCodes.CommitteeDecisionDelete, _audit.StagedEntries[0].EventCode);
        Assert.Equal("delete", _audit.StagedEntries[0].Action);
        Assert.Equal(_decisionId, _audit.StagedEntries[0].EntityId);
    }

    [Fact]
    public async Task DeleteDraft_SubmittedDecision_ReturnsInvalidStatus()
    {
        var decision = await _db.CommitteeDecisions.FirstAsync(d => d.Id == _decisionId);
        decision.Status = CommitteeDecisionStatuses.Submitted;
        await _db.SaveChangesAsync();

        var result = await _service.DeleteDraftAsync(_orgId, _decisionId, 1, _auth);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("INVALID_STATUS", result.Code);
        Assert.True(await _db.CommitteeDecisions.AnyAsync(d => d.Id == _decisionId));
    }

    [Fact]
    public async Task DeleteDraft_ReturnedForRevision_ReturnsInvalidStatus()
    {
        var decision = await _db.CommitteeDecisions.FirstAsync(d => d.Id == _decisionId);
        decision.Status = CommitteeDecisionStatuses.ReturnedForRevision;
        await _db.SaveChangesAsync();

        var result = await _service.DeleteDraftAsync(_orgId, _decisionId, 1, _auth);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("INVALID_STATUS", result.Code);
    }

    [Fact]
    public async Task DeleteDraft_WrongVersion_ReturnsVersionConflict()
    {
        var result = await _service.DeleteDraftAsync(_orgId, _decisionId, 99, _auth);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("VERSION_CONFLICT", result.Code);
    }

    [Fact]
    public async Task DeleteDraft_MissingIfMatch_ReturnsVersionConflict()
    {
        var result = await _service.DeleteDraftAsync(_orgId, _decisionId, null, _auth);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("VERSION_CONFLICT", result.Code);
    }

    [Fact]
    public async Task DeleteDraft_WithoutFamilyScope_ReturnsForbidden()
    {
        var restrictedAuth = new AuthorizationContext
        {
            UserId = _outsiderId,
            SystemRole = Roles.Coordinator,
            OrganizationId = _orgId,
            Grants =
            [
                new GrantContext
                {
                    PermissionKey = PermissionKeys.CommitteeDecisionsEditDraft,
                    Scope = PermissionScopes.MyRecords,
                },
            ],
        };

        var result = await _service.DeleteDraftAsync(_orgId, _decisionId, 1, restrictedAuth);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.Code);
    }

    [Fact]
    public async Task DeleteDraft_MissingDecision_ReturnsNotFound()
    {
        var result = await _service.DeleteDraftAsync(_orgId, Guid.NewGuid(), 1, _auth);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("NOT_FOUND", result.Code);
    }

    private void SeedData()
    {
        var now = DateTime.UtcNow;

        _db.Organizations.Add(new Organization
        {
            Id = _orgId,
            Name = "Org",
            Code = "ORG",
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });

        _db.Users.Add(new User
        {
            Id = _actorId,
            OrganizationId = _orgId,
            Username = "admin",
            PasswordHash = "hash",
            FullName = "Admin",
            Role = Roles.OrganizationAdministrator,
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });

        _db.Families.Add(new Family
        {
            Id = _familyId,
            OrganizationId = _orgId,
            FamilyCode = "F-000001",
            AccountingCode = 1001,
            AccountingCoordinatorId = _actorId,
            AssignedCoordinatorId = _actorId,
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
            TypeCode = "HELP",
            Name = "סיוע",
            Frequency = "monthly",
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });

        _db.CommitteeDecisions.Add(new CommitteeDecision
        {
            Id = _decisionId,
            OrganizationId = _orgId,
            DecisionCode = "D-000001",
            FamilyId = _familyId,
            MeetingDate = DateOnly.FromDateTime(now),
            Status = CommitteeDecisionStatuses.Draft,
            CreatedByUserId = _actorId,
            TotalAmount = 0,
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
