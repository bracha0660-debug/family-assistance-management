using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Tests;

public sealed class AssistanceTypeServiceRelatedSuppliersTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AssistanceTypeService _service;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _activeSupplierId = Guid.NewGuid();
    private readonly Guid _inactiveSupplierId = Guid.NewGuid();
    private readonly Guid _otherOrgSupplierId = Guid.NewGuid();

    public AssistanceTypeServiceRelatedSuppliersTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new AssistanceTypeService(_db, new NoOpAuditService());
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_WithActiveSupplier_PersistsLink()
    {
        var result = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "ELEC",
            Name = "חשמל",
            Frequency = "monthly",
            RelatedSupplierIds = [_activeSupplierId]
        }, _actorId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.RelatedSuppliers);
        Assert.Equal(_activeSupplierId, result.Value.RelatedSuppliers[0].Id);
    }

    [Fact]
    public async Task Create_WithInactiveSupplier_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "WATER",
            Name = "מים",
            Frequency = "monthly",
            RelatedSupplierIds = [_inactiveSupplierId]
        }, _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
        Assert.Equal("ניתן לקשר רק ספק פעיל", result.Error);
    }

    [Fact]
    public async Task Create_WithForeignOrgSupplier_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "GAS",
            Name = "גז",
            Frequency = "monthly",
            RelatedSupplierIds = [_otherOrgSupplierId]
        }, _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
        Assert.Equal("ספק לא נמצא בארגון", result.Error);
    }

    [Fact]
    public async Task Create_WithDuplicateSupplierIds_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "FOOD",
            Name = "מזון",
            Frequency = "monthly",
            RelatedSupplierIds = [_activeSupplierId, _activeSupplierId]
        }, _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
    }

    [Fact]
    public async Task Update_WithOnlyRelatedSupplierIds_BumpsVersion()
    {
        var created = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "RENT",
            Name = "שכירות",
            Frequency = "monthly"
        }, _actorId);
        Assert.True(created.IsSuccess);

        var secondSupplierId = Guid.NewGuid();
        _db.Suppliers.Add(new Supplier
        {
            Id = secondSupplierId,
            OrganizationId = _orgId,
            SupplierCode = "S-002",
            Name = "ספק שני",
            Status = "active",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var updated = await _service.UpdateAsync(
            _orgId,
            created.Value!.Id,
            new UpdateAssistanceTypeRequest { RelatedSupplierIds = [secondSupplierId] },
            created.Value.Version,
            _actorId);

        Assert.True(updated.IsSuccess);
        Assert.Equal(created.Value.Version + 1, updated.Value!.Version);
        Assert.Single(updated.Value.RelatedSuppliers);
        Assert.Equal(secondSupplierId, updated.Value.RelatedSuppliers[0].Id);
    }

    [Fact]
    public async Task Update_WithEmptyRelatedSupplierIds_ClearsLinks()
    {
        var created = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "HEAT",
            Name = "חימום",
            Frequency = "monthly",
            RelatedSupplierIds = [_activeSupplierId]
        }, _actorId);
        Assert.True(created.IsSuccess);
        Assert.Single(created.Value!.RelatedSuppliers);

        var updated = await _service.UpdateAsync(
            _orgId,
            created.Value.Id,
            new UpdateAssistanceTypeRequest { RelatedSupplierIds = [] },
            created.Value.Version,
            _actorId);

        Assert.True(updated.IsSuccess);
        Assert.Empty(updated.Value!.RelatedSuppliers);
    }

    private void SeedData()
    {
        var otherOrgId = Guid.NewGuid();
        _db.Organizations.AddRange(
            new Organization
            {
                Id = _orgId,
                Name = "Org",
                Code = "ORG1",
                Status = "active",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Organization
            {
                Id = otherOrgId,
                Name = "Other",
                Code = "ORG2",
                Status = "active",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        _db.Suppliers.AddRange(
            new Supplier
            {
                Id = _activeSupplierId,
                OrganizationId = _orgId,
                SupplierCode = "S-001",
                Name = "ספק פעיל",
                Status = "active",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Supplier
            {
                Id = _inactiveSupplierId,
                OrganizationId = _orgId,
                SupplierCode = "S-INACTIVE",
                Name = "ספק לא פעיל",
                Status = "inactive",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Supplier
            {
                Id = _otherOrgSupplierId,
                OrganizationId = otherOrgId,
                SupplierCode = "S-OTHER",
                Name = "ספק ארגון אחר",
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
