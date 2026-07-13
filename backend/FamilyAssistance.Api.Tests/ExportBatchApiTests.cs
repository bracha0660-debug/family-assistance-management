using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilyAssistance.Api.Tests;

/// <summary>Phase 16 M94 — payment rows + export batch APIs.</summary>
public sealed class ExportBatchApiTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CapturingAuditService _audit = new();
    private readonly ExportBatchService _export;
    private readonly AssistanceItemService _items;
    private readonly HomeWidgetComposer _home = new();
    private readonly string _uploadRoot;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _typeId = Guid.NewGuid();
    private readonly Guid _decisionId = Guid.NewGuid();
    private readonly Guid _itemA = Guid.NewGuid();
    private readonly Guid _itemB = Guid.NewGuid();
    private readonly AuthorizationContext _financeAuth;

    public ExportBatchApiTests()
    {
        _db = TestDbContextFactory.Create();
        _uploadRoot = Path.Combine(Path.GetTempPath(), "fam-export-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_uploadRoot);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:UploadPath"] = _uploadRoot
            })
            .Build();
        var storage = new DocumentStorageService(config, NullLogger<DocumentStorageService>.Instance);
        _items = new AssistanceItemService(_db, _audit, new AssistanceItemHistoryService(_db));
        _export = new ExportBatchService(_db, _audit, storage, _items, new AssistanceItemHistoryService(_db));
        _financeAuth = new AuthorizationContext
        {
            UserId = _userId,
            SystemRole = Roles.Coordinator,
            OrganizationId = _orgId,
            Grants =
            [
                new GrantContext { PermissionKey = PermissionKeys.PaymentsView, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsExportBatchesCreate, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsExportBatchesDownload, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsExportBatchesCancel, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsExportBatchItemsCancel, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsEnterReference, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.PaymentsEditAssistanceItems, Scope = PermissionScopes.Organization },
                new GrantContext { PermissionKey = PermissionKeys.AssistanceItemsViewHistory, Scope = PermissionScopes.Organization },
            ]
        };
        Seed();
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_uploadRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task ListPaymentRows_IncludesApprovedWaitingPaid()
    {
        var listed = await _export.ListPaymentRowsAsync(_orgId, _financeAuth);
        Assert.True(listed.IsSuccess);
        Assert.Equal(2, listed.Value!.Summary.Approved);
        Assert.Contains(listed.Value.Items, i => i.AssistanceItemId == _itemA && i.EligibleForExport);
    }

    [Fact]
    public async Task CreateBatch_MovesApprovedToWaiting_CreatesPeAndFile()
    {
        var itemA = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var itemB = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemB);

        var created = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items =
            [
                new ExportBatchSelection { AssistanceItemId = _itemA, Version = itemA.Version },
                new ExportBatchSelection { AssistanceItemId = _itemB, Version = itemB.Version },
            ]
        }, _financeAuth);

        Assert.True(created.IsSuccess, created.Error);
        Assert.Equal(ExportBatchStatuses.Open, created.Value!.Status);
        Assert.Equal(2, created.Value.ActiveItemCount);
        Assert.StartsWith("EB-", created.Value.BatchNumber);
        Assert.Contains("download", created.Value.AvailableActions);

        var reloaded = await _db.AssistanceItems.Include(i => i.PaymentExecution).Where(i => i.Id == _itemA || i.Id == _itemB).ToListAsync();
        Assert.All(reloaded, i => Assert.Equal(AssistanceItemStatuses.WaitingForReference, i.Status));
        Assert.All(reloaded, i => Assert.NotNull(i.PaymentExecution));

        var download = await _export.DownloadBatchAsync(_orgId, created.Value.Id, _financeAuth);
        Assert.True(download.IsSuccess);
        await using (download.Value!.Content) { }
    }

    [Fact]
    public async Task CreateBatch_MissingTypeCode_FailsEntireCreate()
    {
        var type = await _db.AssistanceTypes.SingleAsync(t => t.Id == _typeId);
        type.TypeCode = "";
        await _db.SaveChangesAsync();
        var itemA = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);

        var created = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemA, Version = itemA.Version }]
        }, _financeAuth);

        Assert.False(created.IsSuccess);
        Assert.Equal("EXPORT_VALIDATION_ERROR", created.Code);
        Assert.Equal(0, await _db.ExportBatches.CountAsync());
        var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(created.StructuredDetails);
        var messages = rows.Cast<object>().Select(o =>
        {
            var prop = o.GetType().GetProperty("Message");
            return prop?.GetValue(o)?.ToString() ?? "";
        }).ToList();
        Assert.Contains(messages, m => m.Contains("חסר קוד סוג סיוע"));
    }

    [Fact]
    public async Task CancelItem_ReturnsToApproved_AllowsReExport()
    {
        var itemA = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var created = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemA, Version = itemA.Version }]
        }, _financeAuth);
        Assert.True(created.IsSuccess, created.Error);
        var batchItemId = created.Value!.Items!.Single().Id;

        var cancelled = await _export.CancelBatchItemAsync(
            _orgId, created.Value.Id, batchItemId,
            new CancelExportBatchItemRequest { Reason = "תיקון לפני תשלום" },
            _financeAuth);
        Assert.True(cancelled.IsSuccess, cancelled.Error);
        Assert.Equal(ExportBatchStatuses.Cancelled, cancelled.Value!.Status);

        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        Assert.Equal(AssistanceItemStatuses.Approved, item.Status);

        var again = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemA, Version = item.Version }]
        }, _financeAuth);
        Assert.True(again.IsSuccess, again.Error);
        Assert.NotEqual(created.Value.BatchNumber, again.Value!.BatchNumber);
    }

    [Fact]
    public async Task AdjustAmount_PreservesOriginal_RequiresOtherExplanation()
    {
        var itemA = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var bad = await _export.AdjustAmountAsync(
            _orgId, _itemA,
            new AdjustPaymentAmountRequest { NewAmount = 510m, Reason = AmountAdjustmentReasons.Other },
            itemA.Version, _financeAuth);
        Assert.False(bad.IsSuccess);

        var ok = await _export.AdjustAmountAsync(
            _orgId, _itemA,
            new AdjustPaymentAmountRequest
            {
                NewAmount = 510m,
                Reason = AmountAdjustmentReasons.TypingError
            },
            itemA.Version, _financeAuth);
        Assert.True(ok.IsSuccess, ok.Error);
        Assert.Equal(500m, ok.Value!.OriginalApprovedAmount);
        Assert.Equal(510m, ok.Value.Amount);
        Assert.Equal(500m, ok.Value.PreviousPaymentAmount);
    }

    [Fact]
    public void HomeOperationalPaymentCards_TargetPaymentsTab()
    {
        var decision = _db.CommitteeDecisions
            .Include(d => d.Family)
            .Include(d => d.Items)
            .Single(d => d.Id == _decisionId);
        var item = decision.Items.Single(i => i.Id == _itemA);
        item.Status = AssistanceItemStatuses.WaitingForReference;
        _db.SaveChanges();

        var home = _home.Compose(_financeAuth, [decision], [], []);
        var kpiWidget = home.Widgets.First(w => w.Type == HomeWidgetTypes.KpiCards);
        var cards = kpiWidget.Data!.Value.Deserialize<HomeKpiCardsDataDto>(
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var awaiting = cards.Cards.First(c => c.KpiKey == "awaiting_execution");
        Assert.Equal("payments", awaiting.NavigationTarget!.TargetTab);
        Assert.Equal("finance_waiting_for_reference", awaiting.NavigationTarget.Section);
    }

    private void Seed()
    {
        var now = DateTime.UtcNow;
        _db.Organizations.Add(new Organization
        {
            Id = _orgId, Name = "Org", Code = "ORG94", Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.Users.Add(new User
        {
            Id = _userId, OrganizationId = _orgId, Username = "fin94", PasswordHash = "x",
            FullName = "Fin", Role = Roles.Coordinator, Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.Families.Add(new Family
        {
            Id = _familyId, OrganizationId = _orgId, FamilyCode = "F-94", AccountingCode = 9401,
            AccountingCoordinatorId = _userId, AssignedCoordinatorId = _userId, FamilyLastName = "כהן",
            Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.AssistanceTypes.Add(new AssistanceType
        {
            Id = _typeId, OrganizationId = _orgId, TypeCode = "5030", Name = "לימודים",
            Frequency = "one_time", Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.CommitteeDecisions.Add(new CommitteeDecision
        {
            Id = _decisionId, OrganizationId = _orgId, DecisionCode = "D-000094", FamilyId = _familyId,
            MeetingDate = DateOnly.FromDateTime(now), Status = CommitteeDecisionStatuses.Approved,
            CreatedByUserId = _userId, TotalAmount = 1000m, CreatedAt = now, UpdatedAt = now
        });
        _db.AssistanceItems.AddRange(
            MakeItem(_itemA, 1, 500m, now),
            MakeItem(_itemB, 2, 500m, now));
        _db.SaveChanges();
    }

    private AssistanceItem MakeItem(Guid id, int line, decimal amount, DateTime now) => new()
    {
        Id = id,
        OrganizationId = _orgId,
        CommitteeDecisionId = _decisionId,
        LineNumber = line,
        AssistanceTypeId = _typeId,
        Amount = amount,
        OriginalApprovedAmount = amount,
        PaymentTarget = PaymentTargets.Family,
        PaymentMethod = PaymentMethods.BankTransfer,
        Status = AssistanceItemStatuses.Approved,
        ExecutionStatus = PaymentExecutionStatuses.AwaitingPayment,
        ApprovedAt = now,
        Version = 1,
        CreatedAt = now,
        UpdatedAt = now
    };

    private sealed class CapturingAuditService : IAuditService
    {
        public void Stage(AuditEntry entry) { }
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
