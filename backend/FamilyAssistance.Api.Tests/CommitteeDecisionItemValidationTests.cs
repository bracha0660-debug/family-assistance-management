using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using FamilyAssistance.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Tests;

public sealed class CommitteeDecisionItemValidationTests : IDisposable
{
    private const string CompleteBankNumber = "12";
    private const string CompleteBranchNumber = "123";
    private const string CompleteAccountNumber = "456789";
    private const string CompleteAccountHolder = "ישראל ישראלי";

    private readonly AppDbContext _db;
    private readonly CommitteeDecisionService _service;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _assistanceTypeId = Guid.NewGuid();
    private readonly Guid _decisionId = Guid.NewGuid();
    private readonly AuthorizationContext _auth;

    public CommitteeDecisionItemValidationTests()
    {
        _db = TestDbContextFactory.Create();
        _service = new CommitteeDecisionService(_db, new NoOpAuditService());
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
    public async Task AddItem_FamilyBankTransferIncompleteBank_ReturnsFamilyBankMessage()
    {
        var result = await AddItemAsync(PaymentTargets.Family, PaymentMethods.BankTransfer, payeeName: "כהן");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INCOMPLETE_BANK_DETAILS", result.Code);
        Assert.Equal(CommitteeItemPaymentRules.FamilyBankIncompleteMessage, result.Error);
    }

    [Fact]
    public async Task AddItem_SupplierBankTransferIncompleteBank_ReturnsSupplierBankMessage()
    {
        var result = await AddItemAsync(
            PaymentTargets.Supplier,
            PaymentMethods.BankTransfer,
            supplierId: _supplierId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INCOMPLETE_BANK_DETAILS", result.Code);
        Assert.Equal(CommitteeItemPaymentRules.SupplierBankIncompleteMessage, result.Error);
    }

    [Fact]
    public async Task AddItem_SupplierVouchers_ReturnsValidationError()
    {
        SetSupplierBankComplete();

        var result = await AddItemAsync(
            PaymentTargets.Supplier,
            PaymentMethods.Vouchers,
            supplierId: _supplierId,
            voucherType: "מזון");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
        Assert.Equal(CommitteeItemPaymentRules.SupplierVouchersMessage, result.Error);
    }

    [Fact]
    public async Task Submit_AfterFamilyBankCleared_ReturnsFamilyBankMessage()
    {
        SetFamilyBankComplete();

        var addResult = await AddItemAsync(
            PaymentTargets.Family,
            PaymentMethods.Check,
            payeeName: "כהן");
        Assert.True(addResult.IsSuccess);

        var family = await _db.Families.FirstAsync(f => f.Id == _familyId);
        family.BankNumber = null;
        family.BranchNumber = null;
        family.AccountNumber = null;
        family.AccountHolderName = null;
        await _db.SaveChangesAsync();

        var item = await _db.AssistanceItems.FirstAsync(i => i.CommitteeDecisionId == _decisionId);
        item.PaymentMethod = PaymentMethods.BankTransfer;
        item.PayeeName = "כהן";
        await _db.SaveChangesAsync();

        var submitResult = await _service.SubmitAsync(_orgId, _decisionId, 2, _auth);

        Assert.False(submitResult.IsSuccess);
        Assert.Equal(400, submitResult.StatusCode);
        Assert.Equal("INCOMPLETE_BANK_DETAILS", submitResult.Code);
        Assert.Equal(CommitteeItemPaymentRules.FamilyBankIncompleteMessage, submitResult.Error);
    }

    [Fact]
    public async Task AddItem_FamilyCheckWithPayeeName_Succeeds()
    {
        var result = await AddItemAsync(
            PaymentTargets.Family,
            PaymentMethods.Check,
            payeeName: "כהן");

        Assert.True(result.IsSuccess);
        Assert.Equal("כהן", result.Value!.Item.PayeeName);
    }

    [Fact]
    public async Task AddItem_OtherBankTransferWithoutTransferBank_ReturnsValidationError()
    {
        var result = await AddItemAsync(
            PaymentTargets.Other,
            PaymentMethods.BankTransfer,
            payeeName: "מוטב אחר");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
        Assert.Equal(CommitteeItemPaymentRules.TransferBankRequiredMessage, result.Error);
    }

    [Fact]
    public async Task AddItem_OtherBankTransferWithValidTransferBank_Succeeds()
    {
        var result = await AddItemAsync(
            PaymentTargets.Other,
            PaymentMethods.BankTransfer,
            payeeName: "מוטב אחר",
            transferBankNumber: CompleteBankNumber,
            transferBranchNumber: CompleteBranchNumber,
            transferAccountNumber: CompleteAccountNumber);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompleteBankNumber, result.Value!.Item.TransferBankNumber);
        Assert.Equal(CompleteBranchNumber, result.Value!.Item.TransferBranchNumber);
        Assert.Equal(CompleteAccountNumber, result.Value!.Item.TransferAccountNumber);
    }

    [Fact]
    public async Task AddItem_OtherCheck_DoesNotRequireTransferBank()
    {
        var result = await AddItemAsync(
            PaymentTargets.Other,
            PaymentMethods.Check,
            payeeName: "מוטב אחר");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Item.TransferBankNumber);
    }

    [Fact]
    public async Task Submit_AfterTransferBankCleared_ReturnsValidationError()
    {
        var addResult = await AddItemAsync(
            PaymentTargets.Other,
            PaymentMethods.BankTransfer,
            payeeName: "מוטב אחר",
            transferBankNumber: CompleteBankNumber,
            transferBranchNumber: CompleteBranchNumber,
            transferAccountNumber: CompleteAccountNumber);
        Assert.True(addResult.IsSuccess);

        var item = await _db.AssistanceItems.FirstAsync(i => i.CommitteeDecisionId == _decisionId);
        item.TransferBankNumber = null;
        item.TransferBranchNumber = null;
        item.TransferAccountNumber = null;
        await _db.SaveChangesAsync();

        var submitResult = await _service.SubmitAsync(_orgId, _decisionId, 2, _auth);

        Assert.False(submitResult.IsSuccess);
        Assert.Equal(400, submitResult.StatusCode);
        Assert.Equal("VALIDATION_ERROR", submitResult.Code);
        Assert.Equal(CommitteeItemPaymentRules.TransferBankRequiredMessage, submitResult.Error);
    }

    private async Task<ServiceResult<(AssistanceItemDto Item, int DecisionVersion)>> AddItemAsync(
        string paymentTarget,
        string paymentMethod,
        Guid? supplierId = null,
        string? payeeName = null,
        string? voucherType = null,
        string? transferBankNumber = null,
        string? transferBranchNumber = null,
        string? transferAccountNumber = null)
    {
        return await _service.AddItemAsync(
            _orgId,
            _decisionId,
            new CreateAssistanceItemRequest
            {
                AssistanceTypeId = _assistanceTypeId,
                Amount = 500,
                PaymentTarget = paymentTarget,
                PaymentMethod = paymentMethod,
                SupplierId = supplierId,
                PayeeName = payeeName,
                VoucherType = voucherType,
                TransferBankNumber = transferBankNumber,
                TransferBranchNumber = transferBranchNumber,
                TransferAccountNumber = transferAccountNumber,
            },
            1,
            _auth);
    }

    private void SetFamilyBankComplete()
    {
        var family = _db.Families.First(f => f.Id == _familyId);
        family.BankNumber = CompleteBankNumber;
        family.BranchNumber = CompleteBranchNumber;
        family.AccountNumber = CompleteAccountNumber;
        family.AccountHolderName = CompleteAccountHolder;
        _db.SaveChanges();
    }

    private void SetSupplierBankComplete()
    {
        var supplier = _db.Suppliers.First(s => s.Id == _supplierId);
        supplier.BankNumber = CompleteBankNumber;
        supplier.BranchNumber = CompleteBranchNumber;
        supplier.AccountNumber = CompleteAccountNumber;
        supplier.AccountHolderName = CompleteAccountHolder;
        _db.SaveChanges();
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

        _db.Suppliers.Add(new Supplier
        {
            Id = _supplierId,
            OrganizationId = _orgId,
            SupplierCode = "S-000001",
            Name = "ספק בדיקה",
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

    private sealed class NoOpAuditService : IAuditService
    {
        public void Stage(AuditEntry entry) { }
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
