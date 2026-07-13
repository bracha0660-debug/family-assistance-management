using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using FamilyAssistance.Api.Validation;

namespace FamilyAssistance.Api.Tests;

public sealed class SupplierAccountingEmailTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SupplierService _service;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _legacySupplierId = Guid.NewGuid();
    private readonly Guid _codedSupplierId = Guid.NewGuid();

    public SupplierAccountingEmailTests()
    {
        _db = TestDbContextFactory.Create();
        _service = new SupplierService(_db, new NoOpAuditService());
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void ValidateEmail_InvalidFormat_ReturnsInvalidMessage()
    {
        Assert.Equal(EmailValidator.InvalidMessage, EmailValidator.Validate("not-an-email"));
    }

    [Fact]
    public void ValidateEmail_Empty_ReturnsNull()
    {
        Assert.Null(EmailValidator.Validate(null));
        Assert.Null(EmailValidator.Validate(""));
    }

    [Fact]
    public void ValidateEmail_Valid_ReturnsNull()
    {
        Assert.Null(EmailValidator.Validate("supplier@example.com"));
    }

    [Fact]
    public async Task Update_LegacySupplierWithoutAccountingCode_ReturnsValidationError()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _legacySupplierId,
            new UpdateSupplierRequest { Name = "שם חדש" },
            1,
            _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
        Assert.Contains("קוד בהנהלת חשבונות הוא שדה חובה", result.Details);
    }

    [Fact]
    public async Task Update_LegacySupplierWithAccountingCode_Succeeds()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _legacySupplierId,
            new UpdateSupplierRequest { AccountingCode = "ACC-100" },
            1,
            _actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("ACC-100", result.Value!.AccountingCode);
    }

    [Fact]
    public async Task Update_WithInvalidEmail_ReturnsValidationError()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _codedSupplierId,
            new UpdateSupplierRequest { Email = "bad-email" },
            1,
            _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
    }

    [Fact]
    public async Task Update_WithValidEmail_StoresEmail()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _codedSupplierId,
            new UpdateSupplierRequest { Email = "pay@supplier.co.il" },
            1,
            _actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("pay@supplier.co.il", result.Value!.Email);
    }

    [Fact]
    public async Task Update_ClearEmail_Succeeds()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _codedSupplierId,
            new UpdateSupplierRequest { Email = "" },
            1,
            _actorId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Email);
    }

    private void SeedData()
    {
        _db.Organizations.Add(new Organization
        {
            Id = _orgId,
            Name = "Org",
            Code = "ORG2",
            Status = "active",
            Version = 1,
            SupplierCodeCounter = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _db.Suppliers.Add(new Supplier
        {
            Id = _legacySupplierId,
            OrganizationId = _orgId,
            SupplierCode = "S-000001",
            Name = "ספק ללא קוד",
            RegistrationNumber = "514111111",
            AccountingCode = null,
            Email = null,
            Status = "active",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _db.Suppliers.Add(new Supplier
        {
            Id = _codedSupplierId,
            OrganizationId = _orgId,
            SupplierCode = "S-000002",
            Name = "ספק עם קוד",
            RegistrationNumber = "514222222",
            AccountingCode = "ACC-200",
            Email = "old@supplier.co.il",
            Status = "active",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _db.SaveChanges();
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public void Stage(AuditEntry entry) { }
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
