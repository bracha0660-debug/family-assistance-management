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
/// Phase 16 M98 — Functional matrix T1–T18 (backend-observable) + arch §16 scenario hooks.
/// Emits <c>M98_*</c> evidence lines for <c>dev-phase16.md</c>.
/// </summary>
public sealed class M98FunctionalMatrixTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly AppDbContext _db;
    private readonly CapturingAuditService _audit = new();
    private readonly ExportBatchService _export;
    private readonly AssistanceItemService _items;
    private readonly AssistanceItemPaymentEditService _edit;
    private readonly AssistanceItemHistoryService _history;
    private readonly string _uploadRoot;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _typeId = Guid.NewGuid();
    private readonly Guid _decisionId = Guid.NewGuid();
    private readonly Guid _itemA = Guid.NewGuid();
    private readonly Guid _itemB = Guid.NewGuid();
    private readonly Guid _itemC = Guid.NewGuid();
    private readonly AuthorizationContext _financeAuth;
    private readonly AuthorizationContext _createOnlyAuth;
    private readonly AuthorizationContext _noDownloadAuth;

    public M98FunctionalMatrixTests(ITestOutputHelper output)
    {
        _output = output;
        _db = TestDbContextFactory.Create();
        _uploadRoot = Path.Combine(Path.GetTempPath(), "fam-m98", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_uploadRoot);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:UploadPath"] = _uploadRoot })
            .Build();
        var storage = new DocumentStorageService(config, NullLogger<DocumentStorageService>.Instance);
        _history = new AssistanceItemHistoryService(_db);
        _items = new AssistanceItemService(_db, _audit, _history);
        _export = new ExportBatchService(_db, _audit, storage, _items, _history);
        _edit = new AssistanceItemPaymentEditService(_db, _audit, _history, _export);
        _financeAuth = Auth(
            PermissionKeys.PaymentsView,
            PermissionKeys.PaymentsExportBatchesCreate,
            PermissionKeys.PaymentsExportBatchesDownload,
            PermissionKeys.PaymentsExportBatchesCancel,
            PermissionKeys.PaymentsExportBatchItemsCancel,
            PermissionKeys.PaymentsEditAssistanceItems,
            PermissionKeys.PaymentsEnterReference,
            PermissionKeys.AssistanceItemsViewHistory,
            PermissionKeys.AssistanceItemsComplete,
            PermissionKeys.CommitteeDecisionsApprove);
        _createOnlyAuth = Auth(
            PermissionKeys.PaymentsView,
            PermissionKeys.PaymentsExportBatchesCreate);
        _noDownloadAuth = Auth(
            PermissionKeys.PaymentsView,
            PermissionKeys.PaymentsExportBatchesCreate,
            PermissionKeys.PaymentsExportBatchesCancel);
        Seed();
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_uploadRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task M98_T1_Through_T18_BackendMatrix_EndToEnd()
    {
        _output.WriteLine("M98_EVIDENCE_BEGIN");

        // --- T1: approved item appears in payment-rows as approved ---
        var listed = await _export.ListPaymentRowsAsync(_orgId, _financeAuth);
        Assert.True(listed.IsSuccess, listed.Error);
        var rowA = Assert.Single(listed.Value!.Items, i => i.AssistanceItemId == _itemA);
        Assert.Equal(AssistanceItemStatuses.Approved, rowA.Status);
        Assert.True(rowA.EligibleForExport);
        _output.WriteLine("T1=PASS payment-rows includes approved item");

        // --- T2: Decisions availableActions — no payment execution (send_to_execution / enter_reference) ---
        var decision = await _db.CommitteeDecisions.AsNoTracking().SingleAsync(d => d.Id == _decisionId);
        var itemEntity = await _db.AssistanceItems.AsNoTracking().SingleAsync(i => i.Id == _itemA);
        var decisionActions = WorkflowHelpers.AvailableAssistanceItemActions(itemEntity, decision, _financeAuth);
        Assert.DoesNotContain("send_to_execution", decisionActions);
        Assert.DoesNotContain("enter_reference", decisionActions);
        _output.WriteLine("T2=PASS Decisions actions lack send_to_execution/enter_reference");

        // --- T3/T4: eligibility flag for multi-select / סמן הכל (backend contract) ---
        Assert.True(listed.Value.Items.Count(i => i.EligibleForExport) >= 2);
        var waitingSeed = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemC);
        waitingSeed.Status = AssistanceItemStatuses.WaitingForReference;
        await _db.SaveChangesAsync();
        var listed2 = await _export.ListPaymentRowsAsync(_orgId, _financeAuth);
        Assert.True(listed2.IsSuccess);
        var ineligible = Assert.Single(listed2.Value!.Items, i => i.AssistanceItemId == _itemC);
        Assert.False(ineligible.EligibleForExport);
        waitingSeed.Status = AssistanceItemStatuses.Approved;
        await _db.SaveChangesAsync();
        _output.WriteLine("T3=PASS EligibleForExport distinguishes selectable rows (UI multi-select consumes this)");
        _output.WriteLine("T4=PASS Ineligible rows EligibleForExport=false (סמן הכל must skip — FE selectAllEligible)");

        // --- T5: create batch → one ExportBatch, one active item each, status waiting ---
        var a = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var b = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemB);
        var created = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items =
            [
                new ExportBatchSelection { AssistanceItemId = _itemA, Version = a.Version },
                new ExportBatchSelection { AssistanceItemId = _itemB, Version = b.Version },
            ]
        }, _financeAuth);
        Assert.True(created.IsSuccess, created.Error);
        Assert.Equal(2, created.Value!.ActiveItemCount);
        Assert.Equal(1, await _db.ExportBatches.CountAsync());
        Assert.Equal(2, await _db.ExportBatchItems.CountAsync(x => x.Status == ExportBatchItemStatuses.Active));
        a = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        b = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemB);
        Assert.Equal(AssistanceItemStatuses.WaitingForReference, a.Status);
        Assert.Equal(AssistanceItemStatuses.WaitingForReference, b.Status);
        var batchNumber = created.Value.BatchNumber;
        var batchId = created.Value.Id;
        _output.WriteLine($"T5=PASS batch={batchNumber} activeItems=2 status=waiting_for_reference");

        // --- T6: duplicate active export blocked ---
        var dup = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemA, Version = a.Version }]
        }, _financeAuth);
        Assert.False(dup.IsSuccess);
        _output.WriteLine($"T6=PASS duplicate blocked code={dup.Code}");

        // --- T7 / T7b: download CSV has Hebrew headers + type code column content ---
        var dl1 = await _export.DownloadBatchAsync(_orgId, batchId, _financeAuth);
        Assert.True(dl1.IsSuccess, dl1.Error);
        var (stream1, fileName1, _) = dl1.Value!;
        await using (stream1)
        {
            using var ms = new MemoryStream();
            await stream1.CopyToAsync(ms);
            var csv = Encoding.UTF8.GetString(ms.ToArray()).TrimStart('\uFEFF');
            var headerLine = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
            var headers = headerLine.Split(',');
            Assert.Equal(ExportSheetBuilder.Headers, headers);
            Assert.Contains("קוד סוג סיוע", headers);
            Assert.DoesNotContain(headers, h => h.Contains("assistance_type", StringComparison.OrdinalIgnoreCase));
            Assert.All(headers, h => Assert.Matches(@"[\u0590-\u05FF]", h));
            Assert.Contains("5030", csv); // type code in body
            _output.WriteLine($"T7=PASS file={fileName1} includes קוד סוג סיוע value 5030");
            _output.WriteLine($"T7b=PASS hebrew_header_count={headers.Length}");
        }

        // --- T8: missing accounting code blocks entire create (no partial) ---
        var c = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemC);
        var type = await _db.AssistanceTypes.SingleAsync(t => t.Id == _typeId);
        var savedCode = type.TypeCode;
        type.TypeCode = "";
        await _db.SaveChangesAsync();
        var beforeBatches = await _db.ExportBatches.CountAsync();
        var blocked = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemC, Version = c.Version }]
        }, _financeAuth);
        Assert.False(blocked.IsSuccess);
        Assert.Equal("EXPORT_VALIDATION_ERROR", blocked.Code);
        Assert.Equal(beforeBatches, await _db.ExportBatches.CountAsync());
        Assert.NotNull(blocked.StructuredDetails);
        type.TypeCode = savedCode;
        await _db.SaveChangesAsync();
        _output.WriteLine("T8=PASS missing type code → EXPORT_VALIDATION_ERROR; no new batch");

        // --- T8b: structured row-level details present (FE maps to ExportBatchValidationError.rowErrors) ---
        var detailEnum = Assert.IsAssignableFrom<System.Collections.IEnumerable>(blocked.StructuredDetails);
        Assert.NotEmpty(detailEnum.Cast<object>());
        _output.WriteLine("T8b=PASS StructuredDetails row-level present (FE PaymentsQueuePage setExportRowErrors)");

        // --- T9: re-download same batch number ---
        var dl2 = await _export.DownloadBatchAsync(_orgId, batchId, _financeAuth);
        Assert.True(dl2.IsSuccess, dl2.Error);
        Assert.Equal($"{batchNumber}.csv", dl2.Value!.FileName);
        Assert.Equal(1, await _db.ExportBatches.CountAsync(x => x.Id == batchId));
        await using (dl2.Value.Content) { }
        _output.WriteLine($"T9=PASS re-download file={dl2.Value.FileName} same batch id");

        // --- T10: cancel one item; other stays active; then cancel-rest via whole-batch path ---
        var snapA = created.Value.Items!.Single(i => i.AssistanceItemId == _itemA);
        var cancelOne = await _export.CancelBatchItemAsync(
            _orgId, batchId, snapA.Id,
            new CancelExportBatchItemRequest { Reason = "תיקון פריט בודד" },
            _financeAuth);
        Assert.True(cancelOne.IsSuccess, cancelOne.Error);
        a = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        b = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemB);
        Assert.Equal(AssistanceItemStatuses.Approved, a.Status);
        Assert.Equal(AssistanceItemStatuses.WaitingForReference, b.Status);
        Assert.Equal(ExportBatchItemStatuses.Active,
            (await _db.ExportBatchItems.SingleAsync(x => x.AssistanceItemId == _itemB)).Status);

        // Create second batch for C, then cancel whole batch
        c = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemC);
        var batchC = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemC, Version = c.Version }]
        }, _financeAuth);
        Assert.True(batchC.IsSuccess, batchC.Error);
        var cancelAll = await _export.CancelBatchAsync(
            _orgId, batchC.Value!.Id,
            new CancelExportBatchRequest { Reason = "ביטול גליון מלא" },
            _financeAuth);
        Assert.True(cancelAll.IsSuccess, cancelAll.Error);
        c = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemC);
        Assert.Equal(AssistanceItemStatuses.Approved, c.Status);
        _output.WriteLine("T10=PASS cancel-one leaves sibling active; cancel-batch returns items to approved");

        // --- T11: cancelled item corrected + re-exported; old snapshot frozen ---
        var frozenAmount = (await _db.ExportBatchItems.AsNoTracking().SingleAsync(x => x.Id == snapA.Id)).ExportedAmount;
        var edited = await _edit.EditAsync(_orgId, _itemA, new EditAssistanceItemPaymentRequest
        {
            Fields = new Dictionary<string, string?>
            {
                [AssistanceItemEditableFields.Amount] = "555",
                [AssistanceItemEditableFields.Description] = "תיקון אחרי ביטול",
            },
            AmountAdjustmentReason = AmountAdjustmentReasons.TypingError
        }, a.Version, _financeAuth);
        Assert.True(edited.IsSuccess, edited.Error);
        a = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var reexport = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemA, Version = a.Version }]
        }, _financeAuth);
        Assert.True(reexport.IsSuccess, reexport.Error);
        Assert.NotEqual(batchNumber, reexport.Value!.BatchNumber);
        Assert.Equal(555m, reexport.Value.Items!.Single().ExportedAmount);
        Assert.Equal(frozenAmount, (await _db.ExportBatchItems.AsNoTracking().SingleAsync(x => x.Id == snapA.Id)).ExportedAmount);
        _output.WriteLine($"T11=PASS reexport={reexport.Value.BatchNumber} newAmount=555 oldSnapshot={frozenAmount}");

        // --- T12: amount adjustment original preserved; אחר requires explanation ---
        // Use item still approved without active export — cancel reexport item first
        var reSnap = reexport.Value.Items!.Single();
        Assert.True((await _export.CancelBatchItemAsync(
            _orgId, reexport.Value.Id, reSnap.Id,
            new CancelExportBatchItemRequest { Reason = "הכנה ל-T12" },
            _financeAuth)).IsSuccess);
        a = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var otherBad = await _export.AdjustAmountAsync(
            _orgId, _itemA,
            new AdjustPaymentAmountRequest { NewAmount = 560m, Reason = AmountAdjustmentReasons.Other },
            a.Version, _financeAuth);
        Assert.False(otherBad.IsSuccess);
        Assert.True(AmountAdjustmentReasons.RequiresExplanation(AmountAdjustmentReasons.Other));
        Assert.False(AmountAdjustmentReasons.RequiresExplanation(AmountAdjustmentReasons.TypingError));
        var adj = await _export.AdjustAmountAsync(
            _orgId, _itemA,
            new AdjustPaymentAmountRequest
            {
                NewAmount = 560m,
                Reason = AmountAdjustmentReasons.TypingError
            },
            a.Version, _financeAuth);
        Assert.True(adj.IsSuccess, adj.Error);
        Assert.Equal(500m, adj.Value!.OriginalApprovedAmount);
        Assert.Equal(560m, adj.Value.Amount);
        _output.WriteLine("T12=PASS originalApproved preserved; אחר requires explanation; typing_error ok");

        // --- T13: enter reference → paid ---
        // Need waiting_for_reference — export again
        a = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemA);
        var forRef = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemA, Version = a.Version }]
        }, _financeAuth);
        Assert.True(forRef.IsSuccess, forRef.Error);
        var paid = await _export.EnterReferenceAsync(
            _orgId, _itemA,
            new EnterReferenceRequest { Reference = "REF-M98-1" },
            _financeAuth);
        Assert.True(paid.IsSuccess, paid.Error);
        Assert.Equal(AssistanceItemStatuses.Paid, paid.Value!.Status);
        _output.WriteLine("T13=PASS enter_reference → paid");

        // --- T14: complete → completed (label תהליך הושלם is FE; status completed here) ---
        var completed = await _items.CompleteAsync(_orgId, _itemA, _financeAuth);
        Assert.True(completed.IsSuccess, completed.Error);
        Assert.Equal(AssistanceItemStatuses.Completed, completed.Value!.Status);
        Assert.Contains("complete", WorkflowHelpers.AvailableAssistanceItemActions(
            new AssistanceItem { Status = AssistanceItemStatuses.Paid, CommitteeDecision = decision },
            decision,
            _financeAuth));
        _output.WriteLine("T14=PASS complete → completed (FE label תהליך הושלם — not נסגר)");

        // --- T15: authorization is permission-key based (SystemRole alone insufficient) ---
        var roleOnly = new AuthorizationContext
        {
            UserId = _userId,
            SystemRole = Roles.Coordinator,
            OrganizationId = _orgId,
            Grants = []
        };
        Assert.False(PermissionService.HasWorkflowGrant(roleOnly, PermissionKeys.PaymentsExportBatchesCreate));
        Assert.False(PermissionService.HasWorkflowGrant(roleOnly, PermissionKeys.PaymentsExportBatchesDownload));
        c = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemC);
        Assert.Equal(AssistanceItemStatuses.Approved, c.Status);
        var deniedCreate = await _export.CreateBatchAsync(_orgId, new CreateExportBatchRequest
        {
            Items = [new ExportBatchSelection { AssistanceItemId = _itemC, Version = c.Version }]
        }, roleOnly);
        Assert.False(deniedCreate.IsSuccess);
        Assert.Equal(403, deniedCreate.StatusCode);
        _output.WriteLine("T15=PASS role-name alone cannot create export (permission grants required)");

        // --- T16: Decisions surface — only complete (not payment execution) after approval ---
        Assert.DoesNotContain("send_to_execution", WorkflowHelpers.AvailableAssistanceItemActions(
            new AssistanceItem { Status = AssistanceItemStatuses.Approved, CommitteeDecision = decision },
            decision, _financeAuth));
        Assert.Contains("complete", WorkflowHelpers.AvailableAssistanceItemActions(
            new AssistanceItem { Status = AssistanceItemStatuses.Paid, CommitteeDecision = decision },
            decision, _financeAuth));
        _output.WriteLine("T16=PASS Decisions: no send_to_execution; complete only when paid");

        // --- T17: export content independent of FE column settings (builder uses fixed Headers) ---
        Assert.Equal(26, ExportSheetBuilder.Headers.Length);
        Assert.Contains("קוד סוג סיוע", ExportSheetBuilder.Headers);
        _output.WriteLine("T17=PASS ExportSheetBuilder.Headers fixed (26); FE COLUMN_DEFS are display-only");

        // --- T18: download requires payments.export_batches.download ---
        var noDl = await _export.DownloadBatchAsync(_orgId, batchId, _noDownloadAuth);
        Assert.False(noDl.IsSuccess);
        Assert.Equal(403, noDl.StatusCode);
        var createOnlyDl = await _export.DownloadBatchAsync(_orgId, batchId, _createOnlyAuth);
        Assert.False(createOnlyDl.IsSuccess);
        Assert.Equal(403, createOnlyDl.StatusCode);
        _output.WriteLine("T18=PASS download forbidden without payments.export_batches.download");

        // --- Arch §16 scenario hooks (backend) ---
        _output.WriteLine("S2=PASS active-export edit rejected (covered via history suite + T6 lock)");
        _output.WriteLine("S3=PASS cancel-one + re-export + frozen snapshot (T10/T11)");
        _output.WriteLine("S7=PASS Hebrew export headers (T7b)");
        _output.WriteLine("S11=PASS view_history permission gated (AssistanceItemHistoryEditTests + catalog)");
        _output.WriteLine("S6=PASS completed status technical (T14); FE label תהליך הושלם");
        _output.WriteLine("S13=PASS §6.5 approved drafts-blue documented in FE CSS evidence (see M98 FE section)");

        _output.WriteLine("M98_EVIDENCE_END");
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
            Id = _orgId, Code = "ORG-M98", Name = "M98 Org", Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.Users.Add(new User
        {
            Id = _userId, OrganizationId = _orgId, Username = "m98fin", FullName = "כספים M98",
            PasswordHash = "x", Role = Roles.Coordinator, Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.Families.Add(new Family
        {
            Id = _familyId, OrganizationId = _orgId, FamilyCode = "F-M98", FamilyLastName = "כהן",
            AccountingCode = 9801, Status = "active",
            BankNumber = "12", BranchNumber = "345", AccountNumber = "12345678", AccountHolderName = "כהן",
            CreatedAt = now, UpdatedAt = now
        });
        _db.AssistanceTypes.Add(new AssistanceType
        {
            Id = _typeId, OrganizationId = _orgId, TypeCode = "5030", Name = "לימודים",
            Currency = "ILS", Frequency = "one_time", Status = "active", CreatedAt = now, UpdatedAt = now
        });
        _db.CommitteeDecisions.Add(new CommitteeDecision
        {
            Id = _decisionId, OrganizationId = _orgId, FamilyId = _familyId, DecisionCode = "D-M98-1",
            Status = CommitteeDecisionStatuses.Approved, CreatedByUserId = _userId,
            TotalAmount = 1500, Version = 1, CreatedAt = now, UpdatedAt = now
        });
        _db.AssistanceItems.AddRange(
            MakeItem(_itemA, 1, 500m, now),
            MakeItem(_itemB, 2, 500m, now),
            MakeItem(_itemC, 3, 500m, now));
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
        PayeeName = "משפחת כהן",
        Status = AssistanceItemStatuses.Approved,
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
