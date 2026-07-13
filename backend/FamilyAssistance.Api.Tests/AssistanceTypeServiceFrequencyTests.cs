using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Tests;

public sealed class AssistanceTypeServiceFrequencyTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AssistanceTypeService _service;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public AssistanceTypeServiceFrequencyTests()
    {
        _db = TestDbContextFactory.Create();
        _service = new AssistanceTypeService(_db, new NoOpAuditService());
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_WithoutFrequency_DefaultsToOneTime()
    {
        var result = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "ELEC",
            Name = "חשמל"
        }, _actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("one_time", result.Value!.Frequency);
        Assert.Null(result.Value.DefaultAmount);
    }

    [Fact]
    public async Task Create_WithMonthlyFrequency_StoresMonthly()
    {
        var result = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "WATER",
            Name = "מים",
            Frequency = "monthly"
        }, _actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("monthly", result.Value!.Frequency);
    }

    [Fact]
    public async Task Create_WithInvalidFrequency_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "GAS",
            Name = "גז",
            Frequency = "weekly"
        }, _actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Code);
    }

    [Fact]
    public async Task Update_NameOnly_DoesNotChangeStoredAmountOrFrequency()
    {
        var created = await _service.CreateAsync(_orgId, new CreateAssistanceTypeRequest
        {
            TypeCode = "RENT",
            Name = "שכירות",
            Frequency = "monthly",
            DefaultAmount = 500m
        }, _actorId);
        Assert.True(created.IsSuccess);

        var updated = await _service.UpdateAsync(
            _orgId,
            created.Value!.Id,
            new UpdateAssistanceTypeRequest { Name = "שכירות מעודכנת" },
            created.Value.Version,
            _actorId);

        Assert.True(updated.IsSuccess);
        Assert.Equal("שכירות מעודכנת", updated.Value!.Name);
        Assert.Equal("monthly", updated.Value.Frequency);
        Assert.Equal(500m, updated.Value.DefaultAmount);
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
