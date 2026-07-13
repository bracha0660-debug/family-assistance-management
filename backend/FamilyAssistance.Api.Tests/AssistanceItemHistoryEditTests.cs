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

/// <summary>Phase 16 B — allow-list edit + AssistanceItem history + view_history.</summary>
public sealed class AssistanceItemHistoryEditTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CapturingAuditService _audit = new();
    private readonly AssistanceItemHistoryService _history;
    private readonly AssistanceItemPaymentEditService _edit;
    private readonly ExportBatchService _export;
    private readonly string _uploadRoot;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _typeId = Guid.NewGuid();
    private readonly Guid _typeB = Guid.NewGuid();
    private readonly Guid _decisionId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly AuthorizationContext _financeAuth;
    private readonly AuthorizationContext _viewOnlyAuth;

    public AssistanceItemHistoryEditTests()
    {
        _db = TestDbContextFactory.Create();
        _uploadRoot = Path.Combine(Path.GetTempPath(), "fam-hist-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_uploadRoot);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:UploadPath"] = _uploadRoot })
            .Build();
        var storage = new DocumentStorageService(config, NullLogger<DocumentStorageService>.Instance);
        _history = new AssistanceItemHistoryService(_db);
        var items = new AssistanceItemService(_db, _audit, _history);
        _export = new ExportBatchService(_db, _audit, storage, items, _history);
        _edit = new AssistanceItemPaymentEditService(_db, _audit, _history, _export);
        _financeAuth = Auth(
            PermissionKeys.PaymentsView,
            PermissionKeys.PaymentsEditAssistanceItems,
            PermissionKeys.PaymentsExportBatchesCreate,
            PermissionKeys.PaymentsExportBatchesDownload,
            PermissionKeys.PaymentsExportBatchItemsCancel,
            PermissionKeys.AssistanceItemsViewHistory);
        _viewOnlyAuth = Auth(PermissionKeys.PaymentsView);
        Seed();
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_uploadRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Edit_AuthorizedApprovedItem_UpdatesFieldsAndCreatesOneParentEvent()
    {
        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        var result = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?>
            {
                [AssistanceItemEditableFields.Description] = "תיאור חדש",
                [AssistanceItemEditableFields.AssistanceTypeId] = _typeB.ToString("D"),
                [AssistanceItemEditableFields.Amount] = "250.50",
            },
            AmountAdjustmentReason = AmountAdjustmentReasons.QuoteUpdate
        }, item.Version, _financeAuth);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(250.50m, result.Value!.Amount);
        Assert.Contains("edit", result.Value.AvailableActions);
        Assert.Contains("view_history", result.Value.AvailableActions);

        var history = await _history.ListAsync(_orgId, _itemId, new AssistanceItemHistoryListQuery(), _financeAuth);
        Assert.True(history.IsSuccess);
        Assert.Single(history.Value!.Events);
        Assert.Equal(AssistanceItemHistoryEventTypes.ItemEdited, history.Value.Events[0].EventType);
        Assert.Equal(3, history.Value.Events[0].FieldChanges.Count);
        Assert.Equal("כספים בדיקה", history.Value.Events[0].ActorDisplayName);
    }

    [Fact]
    public async Task Edit_BankDetails_MasksSensitiveValuesInHistoryApi()
    {
        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        item.PaymentTarget = PaymentTargets.Other;
        item.PaymentMethod = PaymentMethods.BankTransfer;
        item.PayeeName = "מוטב";
        item.TransferBankNumber = "12";
        item.TransferBranchNumber = "345";
        item.TransferAccountNumber = "1234567890";
        await _db.SaveChangesAsync();

        var result = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?>
            {
                [AssistanceItemEditableFields.AccountNumber] = "999988887777",
                [AssistanceItemEditableFields.AccountHolderName] = "בעל חשבון",
            }
        }, item.Version, _financeAuth);

        Assert.True(result.IsSuccess, result.Error);
        var history = await _history.ListAsync(_orgId, _itemId, new AssistanceItemHistoryListQuery(), _financeAuth);
        var accountChange = history.Value!.Events[0].FieldChanges
            .Single(c => c.FieldKey == AssistanceItemEditableFields.AccountNumber);
        Assert.True(accountChange.IsSensitive);
        Assert.DoesNotContain("1234567890", accountChange.PreviousValue ?? "");
        Assert.DoesNotContain("999988887777", accountChange.NewValue ?? "");
        Assert.EndsWith("7777", accountChange.NewValue);
    }

    [Fact]
    public async Task Edit_UnknownField_Rejected()
    {
        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        var result = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?> { ["family_id"] = Guid.NewGuid().ToString("D") }
        }, item.Version, _financeAuth);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
    }

    [Fact]
    public async Task Edit_ActiveExport_Rejected_AndAllowedAfterCancel()
    {
        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        var created = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemId, Version = item.Version }]
        }, _financeAuth);
        Assert.True(created.IsSuccess, created.Error);

        item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        var locked = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?> { [AssistanceItemEditableFields.Description] = "x" }
        }, item.Version, _financeAuth);
        Assert.False(locked.IsSuccess);
        Assert.Equal("EXPORT_LOCK", locked.Code);

        var row = created.Value!.Items!.Single();
        var cancelled = await _export.CancelBatchItemAsync(
            _orgId, created.Value.Id, row.Id, new CancelExportBatchItemRequest { Reason = "תיקון נתונים" }, _financeAuth);
        Assert.True(cancelled.IsSuccess, cancelled.Error);

        item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        Assert.Equal(AssistanceItemStatuses.Approved, item.Status);
        var edited = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?> { [AssistanceItemEditableFields.Description] = "אחרי ביטול" }
        }, item.Version, _financeAuth);
        Assert.True(edited.IsSuccess, edited.Error);
        Assert.Contains("edit", edited.Value!.AvailableActions);
    }

    [Fact]
    public async Task Edit_PaidOrCompletedOrUnauthorized_Rejected()
    {
        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        item.Status = AssistanceItemStatuses.Paid;
        item.ExecutionReference = "REF-1";
        await _db.SaveChangesAsync();

        var paid = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?> { [AssistanceItemEditableFields.Description] = "no" }
        }, item.Version, _financeAuth);
        Assert.Equal("INVALID_STATUS", paid.Code);

        item.Status = AssistanceItemStatuses.Completed;
        item.Version++;
        await _db.SaveChangesAsync();
        var completed = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?> { [AssistanceItemEditableFields.Description] = "no" }
        }, item.Version, _financeAuth);
        Assert.Equal("INVALID_STATUS", completed.Code);

        item.Status = AssistanceItemStatuses.Approved;
        item.ExecutionReference = null;
        item.Version++;
        await _db.SaveChangesAsync();
        var forbidden = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?> { [AssistanceItemEditableFields.Description] = "no" }
        }, item.Version, _viewOnlyAuth);
        Assert.Equal(403, forbidden.StatusCode);
    }

    [Fact]
    public async Task Edit_ConcurrencyConflict_DoesNotOverwrite()
    {
        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        var stale = await _edit.EditAsync(_orgId, _itemId, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?> { [AssistanceItemEditableFields.Description] = "stale" }
        }, expectedVersion: item.Version - 1, _financeAuth);
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal("VERSION_CONFLICT", stale.Code);
    }

    [Fact]
    public async Task History_Unauthorized_Rejected_AndPaginationHonored()
    {
        await _history.AppendEventAsync(
            _orgId, _itemId, AssistanceItemHistoryEventTypes.Approved, _userId, "בודק",
            null, null, null, null);
        await _db.SaveChangesAsync();

        var denied = await _history.ListAsync(_orgId, _itemId, new AssistanceItemHistoryListQuery(), _viewOnlyAuth);
        Assert.Equal(403, denied.StatusCode);

        for (var i = 0; i < 30; i++)
        {
            await _history.AppendEventAsync(
                _orgId, _itemId, AssistanceItemHistoryEventTypes.ItemEdited, _userId, "בודק",
                null, null, null, null, DateTime.UtcNow.AddMinutes(-i));
        }
        await _db.SaveChangesAsync();

        var page = await _history.ListAsync(
            _orgId, _itemId, new AssistanceItemHistoryListQuery { Limit = 25, Offset = 0 }, _financeAuth);
        Assert.True(page.IsSuccess);
        Assert.Equal(25, page.Value!.Events.Count);
        Assert.True(page.Value.Total >= 31);
        Assert.True(page.Value.Events.Zip(page.Value.Events.Skip(1)).All(pair =>
            pair.First.OccurredAt >= pair.Second.OccurredAt));
    }

    [Fact]
    public async Task AvailableActions_ViewHistory_PresentOnlyWhenGranted()
    {
        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        Assert.Contains("view_history", WorkflowHelpers.AvailablePaymentRowActions(item, null, _financeAuth));
        Assert.DoesNotContain("view_history", WorkflowHelpers.AvailablePaymentRowActions(item, null, _viewOnlyAuth));
        Assert.Contains("edit", WorkflowHelpers.AvailablePaymentRowActions(item, null, _financeAuth));
        Assert.DoesNotContain("edit", WorkflowHelpers.AvailablePaymentRowActions(item, null, _viewOnlyAuth));
    }

    [Fact]
    public void ProcessCompleted_Label_IsTahalichHushtalam()
    {
        Assert.Equal("תהליך הושלם", AssistanceItemHistoryEventTypes.DescriptionHe(AssistanceItemHistoryEventTypes.ProcessCompleted));
        Assert.DoesNotContain("נסגר", AssistanceItemHistoryEventTypes.DescriptionHe(AssistanceItemHistoryEventTypes.ProcessCompleted));
    }

    private AuthorizationContext Auth(params string[] keys) => new()
    {
        UserId = _userId,
        SystemRole = Roles.Coordinator,
        OrganizationId = _orgId,
        Grants = keys.Select(k => new GrantContext { PermissionKey = k, Scope = PermissionScopes.Organization }).ToList()
    };

    private void Seed()
    {
        var now = DateTime.UtcNow;
        _db.Organizations.Add(new Organization
        {
            Id = _orgId, Code = "ORG-H", Name = "Hist Org", Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.Users.Add(new User
        {
            Id = _userId, OrganizationId = _orgId, Username = "fin", FullName = "כספים בדיקה",
            PasswordHash = "x", Role = Roles.Coordinator, Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.Families.Add(new Family
        {
            Id = _familyId, OrganizationId = _orgId, FamilyCode = "F1", FamilyLastName = "כהן",
            AccountingCode = 100, Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.AssistanceTypes.AddRange(
            new AssistanceType
            {
                Id = _typeId, OrganizationId = _orgId, TypeCode = "T1", Name = "סוג א",
                Currency = "ILS", Frequency = "one_time", Status = "active", CreatedAt = now, UpdatedAt = now
            },
            new AssistanceType
            {
                Id = _typeB, OrganizationId = _orgId, TypeCode = "T2", Name = "סוג ב",
                Currency = "ILS", Frequency = "one_time", Status = "active", CreatedAt = now, UpdatedAt = now
            });
        _db.CommitteeDecisions.Add(new CommitteeDecision
        {
            Id = _decisionId, OrganizationId = _orgId, FamilyId = _familyId, DecisionCode = "D-1",
            Status = CommitteeDecisionStatuses.Approved, CreatedByUserId = _userId,
            TotalAmount = 100, Version = 1, CreatedAt = now, UpdatedAt = now
        });
        _db.AssistanceItems.Add(new AssistanceItem
        {
            Id = _itemId, OrganizationId = _orgId, CommitteeDecisionId = _decisionId, LineNumber = 1,
            AssistanceTypeId = _typeId, Amount = 100, PaymentTarget = PaymentTargets.Family,
            PaymentMethod = PaymentMethods.BankTransfer, PayeeName = "משפחת כהן",
            Status = AssistanceItemStatuses.Approved,
            OriginalApprovedAmount = 100, Version = 1, CreatedAt = now, UpdatedAt = now
        });
        _db.SaveChanges();
    }

    private sealed class CapturingAuditService : IAuditService
    {
        public void Stage(AuditEntry entry) { }
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
