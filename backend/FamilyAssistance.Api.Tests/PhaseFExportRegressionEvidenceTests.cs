using System.Globalization;
using System.Text;
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
using Xunit.Abstractions;

namespace FamilyAssistance.Api.Tests;

/// <summary>
/// Phase F — fresh M95 export regression evidence after B–G / Hebrew header lock.
/// Emits structured evidence lines for <c>dev-phase16.md</c>.
/// </summary>
public sealed class PhaseFExportRegressionEvidenceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly AppDbContext _db;
    private readonly CapturingAuditService _audit = new();
    private readonly ExportBatchService _export;
    private readonly AssistanceItemHistoryService _history;
    private readonly AssistanceItemPaymentEditService _edit;
    private readonly string _uploadRoot;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _typeId = Guid.NewGuid();
    private readonly Guid _decisionId = Guid.NewGuid();
    private readonly Guid _itemA = Guid.NewGuid();
    private readonly Guid _itemB = Guid.NewGuid();
    private readonly AuthorizationContext _financeAuth;

    public PhaseFExportRegressionEvidenceTests(ITestOutputHelper output)
    {
        _output = output;
        _db = TestDbContextFactory.Create();
        _uploadRoot = Path.Combine(Path.GetTempPath(), "fam-phasef", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_uploadRoot);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:UploadPath"] = _uploadRoot })
            .Build();
        var storage = new DocumentStorageService(config, NullLogger<DocumentStorageService>.Instance);
        _history = new AssistanceItemHistoryService(_db);
        var items = new AssistanceItemService(_db, _audit, _history);
        _export = new ExportBatchService(_db, _audit, storage, items, _history);
        _edit = new AssistanceItemPaymentEditService(_db, _audit, _history, _export);
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
    public async Task PhaseF_FreshExport_HebrewHeaders_SnapshotIntegrity_DuplicateBlocked()
    {
        var itemA = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var itemB = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemB);

        // Raise current amount on A so export uses current (not silently original).
        itemA.Amount = 175.50m;
        itemA.PreviousPaymentAmount = 100m;
        itemA.OriginalApprovedAmount = 100m;
        itemA.AmountAdjustmentReason = AmountAdjustmentReasons.QuoteUpdate;
        itemA.Version++;
        await _db.SaveChangesAsync();

        var created = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items =
            [
                new ExportBatchSelection { AssistanceItemId = _itemA, Version = itemA.Version },
                new ExportBatchSelection { AssistanceItemId = _itemB, Version = itemB.Version },
            ]
        }, _financeAuth);

        Assert.True(created.IsSuccess, created.Error);
        var batch = created.Value!;
        Assert.Equal(2, batch.ActiveItemCount);

        var downloaded = await _export.DownloadBatchAsync(_orgId, batch.Id, _financeAuth);
        Assert.True(downloaded.IsSuccess, downloaded.Error);
        var (stream, fileName, contentType) = downloaded.Value!;
        Assert.Equal("text/csv; charset=utf-8", contentType);
        Assert.Equal($"{batch.BatchNumber}.csv", fileName);
        await using var ownedStream = stream;
        using var ms = new MemoryStream();
        await ownedStream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var csv = Encoding.UTF8.GetString(bytes);
        // Strip BOM
        if (csv.Length > 0 && csv[0] == '\uFEFF')
            csv = csv[1..];
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3, "header + 2 data rows expected");

        var headers = lines[0].Split(',');
        Assert.Equal(ExportSheetBuilder.Headers.Length, headers.Length);
        Assert.Equal(ExportSheetBuilder.Headers, headers);

        foreach (var h in headers)
        {
            Assert.False(string.IsNullOrWhiteSpace(h));
            Assert.DoesNotContain('_', h); // no snake_case English keys
            Assert.Matches(@"[\u0590-\u05FF]", h); // contains Hebrew
        }

        var rowA = lines.Skip(1).First(l => l.Contains(_itemA.ToString("D"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains("175.5", rowA); // current export amount
        Assert.Contains("100", rowA); // original approved still present

        var snapA = batch.Items!.Single(i => i.AssistanceItemId == _itemA);
        var snapB = batch.Items!.Single(i => i.AssistanceItemId == _itemB);
        Assert.Equal(175.50m, snapA.ExportedAmount);
        Assert.Equal(100m, snapA.OriginalApprovedAmount);
        var frozenExportAmountA = snapA.ExportedAmount;
        var entitySnapA = await _db.ExportBatchItems.AsNoTracking().SingleAsync(x => x.Id == snapA.Id);
        var frozenBankA = entitySnapA.TransferBankNumber;
        var frozenHolderA = entitySnapA.AccountHolderName;

        // Duplicate active export blocked
        itemA = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var dup = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemA, Version = itemA.Version }]
        }, _financeAuth);
        Assert.False(dup.IsSuccess);
        Assert.True(
            dup.Code is "EXPORT_VALIDATION_ERROR" or "DUPLICATE_ACTIVE_EXPORT",
            $"unexpected duplicate code: {dup.Code}");

        // Cancel item A → approved; edit amount; old snapshot must stay frozen
        var cancel = await _export.CancelBatchItemAsync(
            _orgId, batch.Id, snapA.Id,
            new CancelExportBatchItemRequest { Reason = "תיקון לפני ייצוא מחדש" },
            _financeAuth);
        Assert.True(cancel.IsSuccess, cancel.Error);

        itemA = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        Assert.Equal(AssistanceItemStatuses.Approved, itemA.Status);

        var edited = await _edit.EditAsync(_orgId, _itemA, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?>
            {
                [AssistanceItemEditableFields.Amount] = "200",
                [AssistanceItemEditableFields.Description] = "אחרי ביטול ייצוא",
            },
            AmountAdjustmentReason = AmountAdjustmentReasons.TypingError
        }, itemA.Version, _financeAuth);
        Assert.True(edited.IsSuccess, edited.Error);

        var oldBatch = await _export.GetBatchAsync(_orgId, batch.Id, _financeAuth);
        Assert.True(oldBatch.IsSuccess);
        var oldSnapA = oldBatch.Value!.Items!.Single(i => i.AssistanceItemId == _itemA);
        Assert.Equal(ExportBatchItemStatuses.Cancelled, oldSnapA.Status);
        Assert.Equal(frozenExportAmountA, oldSnapA.ExportedAmount);
        var oldEntitySnapA = await _db.ExportBatchItems.AsNoTracking().SingleAsync(x => x.Id == snapA.Id);
        Assert.Equal(frozenBankA, oldEntitySnapA.TransferBankNumber);
        Assert.Equal(frozenHolderA, oldEntitySnapA.AccountHolderName);

        // Re-export A in a new batch → new snapshot with current amount
        itemA = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var reexport = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemA, Version = itemA.Version }]
        }, _financeAuth);
        Assert.True(reexport.IsSuccess, reexport.Error);
        var newSnap = reexport.Value!.Items!.Single();
        Assert.Equal(200m, newSnap.ExportedAmount);
        Assert.NotEqual(batch.Id, reexport.Value.Id);
        Assert.NotEqual(batch.BatchNumber, reexport.Value.BatchNumber);

        // Evidence block for Manager / Phase F (captured from this fresh run)
        var evidence = $"""
            PHASE_F_EVIDENCE_BEGIN
            batch_number={batch.BatchNumber}
            batch_created_at_utc={batch.CreatedAt.ToString("o", CultureInfo.InvariantCulture)}
            export_file_name={fileName}
            content_type={contentType}
            active_item_count={batch.ActiveItemCount}
            hebrew_header_count={headers.Length}
            hebrew_headers={string.Join(" | ", headers)}
            snapshot_item_a_exported_amount={frozenExportAmountA.ToString(CultureInfo.InvariantCulture)}
            snapshot_item_a_original_approved={snapA.OriginalApprovedAmount.ToString(CultureInfo.InvariantCulture)}
            snapshot_item_b_exported_amount={snapB.ExportedAmount.ToString(CultureInfo.InvariantCulture)}
            after_cancel_old_snapshot_exported_amount={oldSnapA.ExportedAmount.ToString(CultureInfo.InvariantCulture)}
            after_edit_item_current_amount=200
            reexport_batch_number={reexport.Value.BatchNumber}
            reexport_snapshot_exported_amount={newSnap.ExportedAmount.ToString(CultureInfo.InvariantCulture)}
            duplicate_active_blocked=true
            english_headers_present=false
            PHASE_F_EVIDENCE_END
            """;
        _output.WriteLine(evidence);
        Assert.Contains("PHASE_F_EVIDENCE_BEGIN", evidence);
        Assert.Contains(batch.BatchNumber, evidence);
    }

    private void Seed()
    {
        var now = DateTime.UtcNow;
        _db.Organizations.Add(new Organization
        {
            Id = _orgId, Code = "ORG-F", Name = "Phase F Org", Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.Users.Add(new User
        {
            Id = _userId, OrganizationId = _orgId, Username = "finf", FullName = "כספים PhaseF",
            PasswordHash = "x", Role = Roles.Coordinator, Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.Families.Add(new Family
        {
            Id = _familyId, OrganizationId = _orgId, FamilyCode = "F-PF", FamilyLastName = "לוי",
            AccountingCode = 501, Status = "active",
            BankNumber = "12", BranchNumber = "345", AccountNumber = "12345678", AccountHolderName = "לוי",
            CreatedAt = now, UpdatedAt = now
        });
        _db.AssistanceTypes.Add(new AssistanceType
        {
            Id = _typeId, OrganizationId = _orgId, TypeCode = "FOOD", Name = "מזון",
            Currency = "ILS", Frequency = "one_time", Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.CommitteeDecisions.Add(new CommitteeDecision
        {
            Id = _decisionId, OrganizationId = _orgId, FamilyId = _familyId, DecisionCode = "D-PF-1",
            Status = CommitteeDecisionStatuses.Approved, CreatedByUserId = _userId,
            TotalAmount = 200, Version = 1, CreatedAt = now, UpdatedAt = now
        });
        _db.AssistanceItems.AddRange(
            new AssistanceItem
            {
                Id = _itemA, OrganizationId = _orgId, CommitteeDecisionId = _decisionId, LineNumber = 1,
                AssistanceTypeId = _typeId, Amount = 100, PaymentTarget = PaymentTargets.Family,
                PaymentMethod = PaymentMethods.BankTransfer, PayeeName = "משפחת לוי",
                Status = AssistanceItemStatuses.Approved, OriginalApprovedAmount = 100,
                Version = 1, CreatedAt = now, UpdatedAt = now
            },
            new AssistanceItem
            {
                Id = _itemB, OrganizationId = _orgId, CommitteeDecisionId = _decisionId, LineNumber = 2,
                AssistanceTypeId = _typeId, Amount = 100, PaymentTarget = PaymentTargets.Family,
                PaymentMethod = PaymentMethods.BankTransfer, PayeeName = "משפחת לוי",
                Status = AssistanceItemStatuses.Approved, OriginalApprovedAmount = 100,
                Version = 1, CreatedAt = now, UpdatedAt = now
            });
        _db.SaveChanges();
    }

    private sealed class CapturingAuditService : IAuditService
    {
        public void Stage(AuditEntry entry) { }
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
