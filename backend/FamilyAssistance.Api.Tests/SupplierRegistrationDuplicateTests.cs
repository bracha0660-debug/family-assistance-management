using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;

namespace FamilyAssistance.Api.Tests;

public sealed class SupplierRegistrationDuplicateTests : IDisposable
{
    private const string RegA = "514111111";
    private const string RegB = "033627865";
    private const string RegC = "514111103";

    private readonly AppDbContext _db;
    private readonly SupplierService _service;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _otherOrgId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public SupplierRegistrationDuplicateTests()
    {
        _db = TestDbContextFactory.Create();
        _service = new SupplierService(_db, new NoOpAuditService());
        SeedOrganizations();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_SecondActiveWithSameRegistration_ReturnsDuplicateError()
    {
        SeedActiveSupplier(_orgId, RegA, "S-000001", Guid.NewGuid());

        var result = await CreateSupplierAsync(RegA, "ספק שני");

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("DUPLICATE_REGISTRATION_NUMBER", result.Code);
    }

    [Fact]
    public async Task Create_InactiveOnlyWithoutAck_ReturnsInactiveConflictWithDetails()
    {
        var inactiveId = Guid.NewGuid();
        SeedInactiveSupplier(_orgId, RegA, "S-000010", inactiveId, version: 3);

        var result = await CreateSupplierAsync(RegA, "ספק חדש");

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("INACTIVE_SUPPLIER_SAME_REGISTRATION", result.Code);
        var details = Assert.IsType<InactiveSupplierConflictDetails>(result.StructuredDetails);
        Assert.Equal(inactiveId, details.ExistingSupplierId);
        Assert.Equal("S-000010", details.ExistingSupplierCode);
        Assert.Equal(3, details.ExistingVersion);
    }

    [Fact]
    public async Task Create_InactiveOnlyWithAck_DoesNotReturnInactiveConflict()
    {
        SeedInactiveSupplier(_orgId, RegA, "S-000010", Guid.NewGuid());

        var result = await CreateSupplierAsync(RegA, "ספק חדש", acknowledgeInactiveDuplicate: true);

        Assert.NotEqual("INACTIVE_SUPPLIER_SAME_REGISTRATION", result.Code);
    }

    [Fact]
    public void AcknowledgedCreate_AllowsActiveAndInactiveWithSameRegistration()
    {
        SeedInactiveSupplier(_orgId, RegA, "S-000010", Guid.NewGuid());
        SeedActiveSupplier(_orgId, RegA, "S-000099", Guid.NewGuid(), name: "ספק חדש");

        Assert.Equal(1, _db.Suppliers.Count(s => s.OrganizationId == _orgId && s.RegistrationNumber == RegA && s.Status == "active"));
        Assert.Equal(1, _db.Suppliers.Count(s => s.OrganizationId == _orgId && s.RegistrationNumber == RegA && s.Status == "inactive"));
    }

    [Fact]
    public async Task Create_MultipleInactiveSameReg_ReturnsNewestInactiveDetails()
    {
        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var olderTime = DateTime.UtcNow.AddDays(-2);
        var newerTime = DateTime.UtcNow.AddDays(-1);

        SeedInactiveSupplier(_orgId, RegA, "S-000011", olderId, updatedAt: olderTime, createdAt: olderTime);
        SeedInactiveSupplier(_orgId, RegA, "S-000012", newerId, updatedAt: newerTime, createdAt: newerTime);

        var result = await CreateSupplierAsync(RegA, "ספק חדש");

        Assert.False(result.IsSuccess);
        var details = Assert.IsType<InactiveSupplierConflictDetails>(result.StructuredDetails);
        Assert.Equal(newerId, details.ExistingSupplierId);
        Assert.Equal("S-000012", details.ExistingSupplierCode);
    }

    [Fact]
    public async Task Update_ChangeRegistrationToActiveOther_ReturnsDuplicateError()
    {
        var supplierA = Guid.NewGuid();
        var supplierB = Guid.NewGuid();
        SeedActiveSupplier(_orgId, RegA, "S-000020", supplierA);
        SeedActiveSupplier(_orgId, RegB, "S-000021", supplierB);

        var result = await _service.UpdateAsync(
            _orgId,
            supplierA,
            new UpdateSupplierRequest { RegistrationNumber = RegB, Reason = "שינוי ח.פ לבדיקה" },
            1,
            _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("DUPLICATE_REGISTRATION_NUMBER", result.Code);
    }

    [Fact]
    public async Task Update_KeepOwnRegistration_Succeeds()
    {
        var supplierId = Guid.NewGuid();
        SeedActiveSupplier(_orgId, RegA, "S-000030", supplierId, name: "שם מקורי");

        var result = await _service.UpdateAsync(
            _orgId,
            supplierId,
            new UpdateSupplierRequest { Name = "שם מעודכן" },
            1,
            _actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("שם מעודכן", result.Value!.Name);
        Assert.Equal(RegA, result.Value.RegistrationNumber);
    }

    [Fact]
    public async Task Restore_WhenActiveConflictExists_ReturnsDuplicateError()
    {
        var inactiveId = Guid.NewGuid();
        SeedInactiveSupplier(_orgId, RegA, "S-000040", inactiveId);
        SeedActiveSupplier(_orgId, RegA, "S-000041", Guid.NewGuid());

        var result = await _service.RestoreAsync(
            _orgId,
            inactiveId,
            new RestoreSupplierRequest { Reason = "בדיקת שחזור" },
            1,
            _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("DUPLICATE_REGISTRATION_NUMBER", result.Code);
    }

    [Fact]
    public async Task Restore_WhenNoActiveConflict_Succeeds()
    {
        var inactiveId = Guid.NewGuid();
        SeedInactiveSupplier(_orgId, RegA, "S-000050", inactiveId);

        var result = await _service.RestoreAsync(
            _orgId,
            inactiveId,
            new RestoreSupplierRequest { Reason = "החזרה לפעילות" },
            1,
            _actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("active", result.Value!.Status);
    }

    [Fact]
    public async Task Create_SameRegistrationDifferentOrg_DoesNotReturnDuplicateConflict()
    {
        SeedActiveSupplier(_otherOrgId, RegA, "S-000060", Guid.NewGuid());

        var result = await CreateSupplierAsync(RegA, "ספק בארגון אחר");

        Assert.NotEqual("DUPLICATE_REGISTRATION_NUMBER", result.Code);
        Assert.NotEqual("INACTIVE_SUPPLIER_SAME_REGISTRATION", result.Code);
    }

    private async Task<ServiceResult<SupplierDto>> CreateSupplierAsync(
        string registrationNumber,
        string name,
        bool acknowledgeInactiveDuplicate = false)
    {
        return await _service.CreateAsync(_orgId, new CreateSupplierRequest
        {
            Name = name,
            RegistrationNumber = registrationNumber,
            AccountingCode = "ACC-100",
            AcknowledgeInactiveDuplicate = acknowledgeInactiveDuplicate
        }, _actorId);
    }

    private void SeedOrganizations()
    {
        var now = DateTime.UtcNow;
        _db.Organizations.AddRange(
            new Organization
            {
                Id = _orgId,
                Name = "Org A",
                Code = "ORGA",
                Status = "active",
                Version = 1,
                SupplierCodeCounter = 100,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Organization
            {
                Id = _otherOrgId,
                Name = "Org B",
                Code = "ORGB",
                Status = "active",
                Version = 1,
                SupplierCodeCounter = 200,
                CreatedAt = now,
                UpdatedAt = now
            });
        _db.SaveChanges();
    }

    private void SeedActiveSupplier(
        Guid orgId,
        string registrationNumber,
        string supplierCode,
        Guid id,
        string name = "ספק פעיל")
    {
        var now = DateTime.UtcNow;
        _db.Suppliers.Add(new Supplier
        {
            Id = id,
            OrganizationId = orgId,
            SupplierCode = supplierCode,
            Name = name,
            RegistrationNumber = registrationNumber,
            AccountingCode = "ACC-001",
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.SaveChanges();
    }

    private void SeedInactiveSupplier(
        Guid orgId,
        string registrationNumber,
        string supplierCode,
        Guid id,
        int version = 1,
        DateTime? updatedAt = null,
        DateTime? createdAt = null)
    {
        var now = DateTime.UtcNow;
        _db.Suppliers.Add(new Supplier
        {
            Id = id,
            OrganizationId = orgId,
            SupplierCode = supplierCode,
            Name = "ספק מושבת",
            RegistrationNumber = registrationNumber,
            AccountingCode = "ACC-002",
            Status = "inactive",
            Version = version,
            CreatedAt = createdAt ?? now,
            UpdatedAt = updatedAt ?? now
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
