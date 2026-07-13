using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Tests;

/// <summary>Phase 16 M92 — ExportBatch domain model + amount-adjustment persistence.</summary>
public sealed class ExportBatchDomainTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _typeId = Guid.NewGuid();
    private readonly Guid _decisionId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _paymentId = Guid.NewGuid();

    public ExportBatchDomainTests()
    {
        _db = TestDbContextFactory.Create();
        SeedBase();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Model_HasFilteredUniqueIndex_OnActiveExportBatchItemPaymentExecution()
    {
        var entity = _db.Model.FindEntityType(typeof(ExportBatchItem));
        Assert.NotNull(entity);

        var index = entity!.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "ux_export_batch_items_active_payment_execution");

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.Equal("status = 'active'", index.GetFilter());
        Assert.Equal(
            new[] { nameof(ExportBatchItem.PaymentExecutionId) },
            index.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Model_ExportBatchItem_FksUseRestrict_ToPreserveHistory()
    {
        var entity = _db.Model.FindEntityType(typeof(ExportBatchItem));
        Assert.NotNull(entity);

        var batchFk = entity!.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(ExportBatchItem.ExportBatchId)));
        var paymentFk = entity.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(ExportBatchItem.PaymentExecutionId)));
        var itemFk = entity.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(ExportBatchItem.AssistanceItemId)));

        Assert.Equal(DeleteBehavior.Restrict, batchFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, paymentFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, itemFk.DeleteBehavior);
    }

    [Fact]
    public async Task SoftCancel_PreservesBatchAndItemHistory_AllowsReExportAfterCancel()
    {
        var batch1 = new ExportBatch
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            BatchNumber = "EB-000001",
            Status = ExportBatchStatuses.Open,
            CreatedByUserId = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalItemCount = 1,
            ActiveItemCount = 1,
            CancelledItemCount = 0
        };
        var row1 = new ExportBatchItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            ExportBatchId = batch1.Id,
            PaymentExecutionId = _paymentId,
            AssistanceItemId = _itemId,
            ExportedAmount = 500m,
            Status = ExportBatchItemStatuses.Active,
            DecisionCode = "D-000001",
            FamilyCode = "F-1",
            FamilyAccountingCode = 1001,
            FamilyName = "Cohen",
            AssistanceTypeName = "לימודים",
            AssistanceTypeCode = "5030",
            OriginalApprovedAmount = 500m,
            PaymentTarget = PaymentTargets.Family,
            PaymentMethod = PaymentMethods.BankTransfer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ExportBatches.Add(batch1);
        _db.ExportBatchItems.Add(row1);
        await _db.SaveChangesAsync();

        // Soft cancel — never delete
        row1.Status = ExportBatchItemStatuses.Cancelled;
        row1.CancelledByUserId = _userId;
        row1.CancelledAt = DateTime.UtcNow;
        row1.CancelReason = "תיקון סכום";
        row1.UpdatedAt = DateTime.UtcNow;
        batch1.Status = ExportBatchStatuses.Cancelled;
        batch1.ActiveItemCount = 0;
        batch1.CancelledItemCount = 1;
        batch1.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.ExportBatches.CountAsync());
        Assert.Equal(1, await _db.ExportBatchItems.CountAsync());
        Assert.Equal(ExportBatchItemStatuses.Cancelled, (await _db.ExportBatchItems.SingleAsync()).Status);

        // After cancel, same PaymentExecution may join a new batch as active (history retained).
        var batch2 = new ExportBatch
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            BatchNumber = "EB-000002",
            Status = ExportBatchStatuses.Open,
            CreatedByUserId = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalItemCount = 1,
            ActiveItemCount = 1,
            CancelledItemCount = 0
        };
        _db.ExportBatches.Add(batch2);
        _db.ExportBatchItems.Add(new ExportBatchItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            ExportBatchId = batch2.Id,
            PaymentExecutionId = _paymentId,
            AssistanceItemId = _itemId,
            ExportedAmount = 503m,
            Status = ExportBatchItemStatuses.Active,
            DecisionCode = "D-000001",
            FamilyCode = "F-1",
            FamilyAccountingCode = 1001,
            FamilyName = "Cohen",
            AssistanceTypeName = "לימודים",
            AssistanceTypeCode = "5030",
            OriginalApprovedAmount = 500m,
            AmountAdjustmentReason = AmountAdjustmentReasons.TypingError,
            PaymentTarget = PaymentTargets.Family,
            PaymentMethod = PaymentMethods.BankTransfer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        Assert.Equal(2, await _db.ExportBatches.CountAsync());
        Assert.Equal(2, await _db.ExportBatchItems.CountAsync());
        Assert.Equal(1, await _db.ExportBatchItems.CountAsync(i => i.Status == ExportBatchItemStatuses.Active));
        Assert.Equal(1, await _db.ExportBatchItems.CountAsync(i => i.Status == ExportBatchItemStatuses.Cancelled));
    }

    [Fact]
    public async Task AmountAdjustment_PreservesOriginalApprovedAmount()
    {
        var item = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        item.Status = AssistanceItemStatuses.Approved;
        item.ApprovedAt = DateTime.UtcNow;
        item.OriginalApprovedAmount = 500m;
        item.Amount = 500m;
        await _db.SaveChangesAsync();

        // Simulate adjustment (API arrives in M94): current Amount changes; original stays.
        item.PreviousPaymentAmount = item.Amount;
        item.Amount = 503m;
        item.AmountAdjustmentReason = AmountAdjustmentReasons.TypingError;
        item.AmountAdjustmentExplanation = null;
        item.AmountAdjustedByUserId = _userId;
        item.AmountAdjustedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var reloaded = await _db.AssistanceItems.SingleAsync(i => i.Id == _itemId);
        Assert.Equal(500m, reloaded.OriginalApprovedAmount);
        Assert.Equal(503m, reloaded.Amount);
        Assert.Equal(500m, reloaded.PreviousPaymentAmount);
        Assert.Equal(AmountAdjustmentReasons.TypingError, reloaded.AmountAdjustmentReason);
        Assert.Null(reloaded.AmountAdjustmentExplanation);
    }

    [Fact]
    public void AmountAdjustmentReasons_OtherRequiresExplanation()
    {
        Assert.True(AmountAdjustmentReasons.RequiresExplanation(AmountAdjustmentReasons.Other));
        Assert.False(AmountAdjustmentReasons.RequiresExplanation(AmountAdjustmentReasons.TypingError));
        Assert.False(AmountAdjustmentReasons.RequiresExplanation(AmountAdjustmentReasons.QuoteUpdate));
        Assert.False(AmountAdjustmentReasons.RequiresExplanation(AmountAdjustmentReasons.QuantityChange));
    }

    [Fact]
    public void AccountingCodeSources_ConfirmedFromM91_NoNewFields()
    {
        // Compile-time / model presence check — M91 A1: reuse existing fields only.
        Assert.NotNull(typeof(Family).GetProperty(nameof(Family.AccountingCode)));
        Assert.NotNull(typeof(Supplier).GetProperty(nameof(Supplier.AccountingCode)));
        Assert.NotNull(typeof(AssistanceType).GetProperty(nameof(AssistanceType.TypeCode)));
        Assert.NotNull(typeof(ExportBatchItem).GetProperty(nameof(ExportBatchItem.AssistanceTypeCode)));
        Assert.NotNull(typeof(ExportBatchItem).GetProperty(nameof(ExportBatchItem.FamilyAccountingCode)));
        Assert.NotNull(typeof(ExportBatchItem).GetProperty(nameof(ExportBatchItem.SupplierAccountingCode)));
    }

    private void SeedBase()
    {
        var now = DateTime.UtcNow;
        _db.Organizations.Add(new Organization
        {
            Id = _orgId,
            Name = "Org",
            Code = "ORG16",
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.Users.Add(new User
        {
            Id = _userId,
            OrganizationId = _orgId,
            Username = "finance16",
            PasswordHash = "x",
            FullName = "Finance",
            Role = "coordinator",
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.Families.Add(new Family
        {
            Id = _familyId,
            OrganizationId = _orgId,
            FamilyCode = "F-1",
            AccountingCode = 1001,
            AccountingCoordinatorId = _userId,
            FamilyLastName = "Cohen",
            AssignedCoordinatorId = _userId,
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.AssistanceTypes.Add(new AssistanceType
        {
            Id = _typeId,
            OrganizationId = _orgId,
            TypeCode = "5030",
            Name = "לימודים",
            Frequency = "one_time",
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.CommitteeDecisions.Add(new CommitteeDecision
        {
            Id = _decisionId,
            OrganizationId = _orgId,
            DecisionCode = "D-000001",
            FamilyId = _familyId,
            MeetingDate = DateOnly.FromDateTime(now),
            Status = CommitteeDecisionStatuses.Approved,
            CreatedByUserId = _userId,
            TotalAmount = 500m,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.AssistanceItems.Add(new AssistanceItem
        {
            Id = _itemId,
            OrganizationId = _orgId,
            CommitteeDecisionId = _decisionId,
            LineNumber = 1,
            AssistanceTypeId = _typeId,
            Amount = 500m,
            PaymentTarget = PaymentTargets.Family,
            PaymentMethod = PaymentMethods.BankTransfer,
            Status = AssistanceItemStatuses.Approved,
            ExecutionStatus = PaymentExecutionStatuses.AwaitingPayment,
            ApprovedAt = now,
            OriginalApprovedAmount = 500m,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.PaymentExecutions.Add(new PaymentExecution
        {
            Id = _paymentId,
            OrganizationId = _orgId,
            CommitteeDecisionId = _decisionId,
            AssistanceItemId = _itemId,
            Status = PaymentExecutionStatuses.WaitingForReference,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.SaveChanges();
    }
}
