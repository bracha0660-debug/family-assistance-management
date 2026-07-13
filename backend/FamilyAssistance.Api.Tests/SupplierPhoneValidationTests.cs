using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using FamilyAssistance.Api.Validation;

namespace FamilyAssistance.Api.Tests;

public sealed class SupplierPhoneValidationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SupplierService _service;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    public SupplierPhoneValidationTests()
    {
        _db = TestDbContextFactory.Create();
        _service = new SupplierService(_db, new NoOpAuditService());
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Validate_EmptyPhone_ReturnsNull()
    {
        Assert.Null(IsraeliPhoneValidator.Validate(null));
        Assert.Null(IsraeliPhoneValidator.Validate(""));
        Assert.Null(IsraeliPhoneValidator.Validate("   "));
    }

    [Fact]
    public void Validate_ValidPhone_ReturnsNull()
    {
        Assert.Null(IsraeliPhoneValidator.Validate("054-1234567"));
    }

    [Fact]
    public void Validate_PrefixOnly_ReturnsPrefixOrNumberError()
    {
        var error = IsraeliPhoneValidator.Validate("054");
        Assert.NotNull(error);
        Assert.Equal(IsraeliPhoneValidator.NumberRequiredMessage, error);
    }

    [Fact]
    public void Validate_NumberOnly_ReturnsPrefixError()
    {
        var error = IsraeliPhoneValidator.Validate("1234567");
        Assert.NotNull(error);
        Assert.Equal(IsraeliPhoneValidator.PrefixRequiredMessage, error);
    }

    [Fact]
    public void Validate_InvalidPrefixLength_ReturnsPrefixLengthError()
    {
        var error = IsraeliPhoneValidator.Validate("1-1234567");
        Assert.NotNull(error);
        Assert.Equal(IsraeliPhoneValidator.PrefixLengthMessage, error);
    }

    [Fact]
    public void Validate_InvalidNumberLength_ReturnsNumberLengthError()
    {
        var error = IsraeliPhoneValidator.Validate("054-12345");
        Assert.NotNull(error);
        Assert.Equal(IsraeliPhoneValidator.NumberLengthMessage, error);
    }

    [Fact]
    public async Task Update_WithoutPhone_KeepsPhoneNull()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _supplierId,
            new UpdateSupplierRequest { Name = "ספק מעודכן" },
            1,
            _actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("ספק מעודכן", result.Value!.Name);
        Assert.Null(result.Value.Phone);
    }

    [Fact]
    public async Task Update_WithValidPhone_StoresFormattedPhone()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _supplierId,
            new UpdateSupplierRequest { Phone = "054-1234567" },
            1,
            _actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("054-1234567", result.Value!.Phone);
    }

    [Fact]
    public async Task Update_WithPrefixOnlyPhone_ReturnsValidationError()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _supplierId,
            new UpdateSupplierRequest { Phone = "054" },
            1,
            _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
    }

    [Fact]
    public async Task Update_WithNumberOnlyPhone_ReturnsValidationError()
    {
        var result = await _service.UpdateAsync(
            _orgId,
            _supplierId,
            new UpdateSupplierRequest { Phone = "1234567" },
            1,
            _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
    }

    private void SeedData()
    {
        _db.Organizations.Add(new Organization
        {
            Id = _orgId,
            Name = "Org",
            Code = "ORG1",
            Status = "active",
            Version = 1,
            SupplierCodeCounter = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _db.Suppliers.Add(new Supplier
        {
            Id = _supplierId,
            OrganizationId = _orgId,
            SupplierCode = "S-000001",
            Name = "ספק בדיקה",
            RegistrationNumber = "514111111",
            AccountingCode = "ACC-PHONE",
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
