using System.Text.Json;
using System.Text.RegularExpressions;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class AssistanceTypeService(
    AppDbContext db,
    IAuditService auditService)
{
    private static readonly Regex TypeCodeRegex = new("^[A-Z0-9-]{2,50}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedFrequencies =
    [
        "one_time",
        "monthly",
        "quarterly",
        "annual"
    ];

    public async Task<AssistanceTypeListResponse> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var types = await db.AssistanceTypes
            .Where(t => t.OrganizationId == organizationId)
            .OrderBy(t => t.TypeCode)
            .ToListAsync(cancellationToken);

        var relatedByType = await LoadRelatedSuppliersByTypeAsync(organizationId, cancellationToken);
        var dtos = types.Select(t => Map(t, relatedByType.GetValueOrDefault(t.Id, []))).ToList();

        var total = dtos.Count;
        var active = dtos.Count(t => t.Status == "active");
        var inactive = dtos.Count(t => t.Status == "inactive");

        return new AssistanceTypeListResponse
        {
            Summary = new AssistanceTypeSummaryDto
            {
                Total = total,
                Active = active,
                Inactive = inactive
            },
            AssistanceTypes = dtos
        };
    }

    public async Task<ServiceResult<AssistanceTypeDto>> CreateAsync(
        Guid organizationId,
        CreateAssistanceTypeRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
            return ServiceResult<AssistanceTypeDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        if (request.RelatedSupplierIds is { Count: > 0 })
        {
            var duplicateError = ValidateNoDuplicateSupplierIds(request.RelatedSupplierIds);
            if (duplicateError is not null)
                return ServiceResult<AssistanceTypeDto>.Fail(400, "VALIDATION_ERROR", duplicateError);
        }

        var code = request.TypeCode.Trim().ToUpperInvariant();
        if (await db.AssistanceTypes.AnyAsync(
                t => t.OrganizationId == organizationId && t.TypeCode == code, cancellationToken))
        {
            return ServiceResult<AssistanceTypeDto>.Fail(409, "DUPLICATE_TYPE_CODE", "קוד סוג סיוע כבר קיים");
        }

        var now = DateTime.UtcNow;
        var frequency = ResolveCreateFrequency(request.Frequency);
        var type = new AssistanceType
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            TypeCode = code,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            DefaultAmount = request.DefaultAmount,
            Currency = "ILS",
            Frequency = frequency,
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.AssistanceTypes.Add(type);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceTypeCreate,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "assistance_type",
                EntityId = type.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new
                {
                    type.TypeCode,
                    type.Name,
                    type.Frequency,
                    type.DefaultAmount,
                    type.Currency
                })
            });

            if (request.RelatedSupplierIds is { Count: > 0 })
            {
                await SyncRelatedSuppliersAsync(
                    organizationId, type.Id, request.RelatedSupplierIds, actorUserId, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<AssistanceTypeDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<AssistanceTypeDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        var related = await GetRelatedSuppliersAsync(organizationId, type.Id, cancellationToken);
        return ServiceResult<AssistanceTypeDto>.Ok(Map(type, related));
    }

    public async Task<ServiceResult<AssistanceTypeDto>> GetAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var type = await db.AssistanceTypes
            .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == organizationId, cancellationToken);
        if (type is null)
            return ServiceResult<AssistanceTypeDto>.Fail(404, "NOT_FOUND", "סוג הסיוע לא נמצא");

        var related = await GetRelatedSuppliersAsync(organizationId, id, cancellationToken);
        return ServiceResult<AssistanceTypeDto>.Ok(Map(type, related));
    }

    public async Task<ServiceResult<AssistanceTypeDto>> UpdateAsync(
        Guid organizationId,
        Guid id,
        UpdateAssistanceTypeRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<AssistanceTypeDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var type = await db.AssistanceTypes.FirstOrDefaultAsync(
            t => t.Id == id && t.OrganizationId == organizationId, cancellationToken);
        if (type is null)
            return ServiceResult<AssistanceTypeDto>.Fail(404, "NOT_FOUND", "סוג הסיוע לא נמצא");

        if (type.Status != "active")
            return ServiceResult<AssistanceTypeDto>.Fail(409, "TYPE_INACTIVE", "סוג הסיוע אינו פעיל");

        if (type.Version != expectedVersion)
            return ServiceResult<AssistanceTypeDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var changes = new List<(string Field, string? Old, string? New)>();
        var errors = new List<string>();

        if (request.Name is not null)
        {
            var newName = request.Name.Trim();
            if (newName.Length < 2 || newName.Length > 200)
                errors.Add("שם סוג הסיוע הוא שדה חובה");
            else if (newName != type.Name)
            {
                changes.Add(("name", type.Name, newName));
                type.Name = newName;
            }
        }

        if (request.Description is not null)
        {
            var newDesc = NormalizeOptional(request.Description);
            if (newDesc is not null && newDesc.Length > 1000)
                errors.Add("תיאור חייב להיות עד 1000 תווים");
            else if (newDesc != type.Description)
            {
                changes.Add(("description", type.Description, newDesc));
                type.Description = newDesc;
            }
        }

        if (request.ClearDefaultAmount)
        {
            if (type.DefaultAmount is not null)
            {
                changes.Add(("default_amount", type.DefaultAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture), null));
                type.DefaultAmount = null;
            }
        }
        else if (request.DefaultAmount is not null)
        {
            var newAmount = request.DefaultAmount.Value;
            if (newAmount < 0 || newAmount > 1000000)
                errors.Add("סכום ברירת מחדל חייב להיות בין 0 ל-1,000,000");
            else if (newAmount != type.DefaultAmount)
            {
                changes.Add(("default_amount",
                    type.DefaultAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    newAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                type.DefaultAmount = newAmount;
            }
        }

        if (request.Frequency is not null)
        {
            var newFreq = request.Frequency.Trim();
            if (!AllowedFrequencies.Contains(newFreq))
                errors.Add("תדירות לא חוקית");
            else if (newFreq != type.Frequency)
            {
                changes.Add(("frequency", type.Frequency, newFreq));
                type.Frequency = newFreq;
            }
        }

        if (errors.Count > 0)
            return ServiceResult<AssistanceTypeDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var willSyncLinks = request.RelatedSupplierIds is not null;
        if (willSyncLinks)
        {
            var duplicateError = ValidateNoDuplicateSupplierIds(request.RelatedSupplierIds!);
            if (duplicateError is not null)
                return ServiceResult<AssistanceTypeDto>.Fail(400, "VALIDATION_ERROR", duplicateError);
        }

        if (changes.Count == 0 && !willSyncLinks)
            return ServiceResult<AssistanceTypeDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        if (changes.Count > 0)
        {
            type.Version++;
            type.UpdatedAt = DateTime.UtcNow;
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var change in changes)
            {
                auditService.Stage(new AuditEntry
                {
                    EventCode = BusinessEventCodes.AssistanceTypeUpdate,
                    OrganizationId = organizationId,
                    ActorUserId = actorUserId,
                    EntityType = "assistance_type",
                    EntityId = type.Id,
                    Action = "update",
                    FieldName = change.Field,
                    OldValue = change.Old,
                    NewValue = change.New
                });
            }

            if (willSyncLinks)
            {
                var linksChanged = await SyncRelatedSuppliersAsync(
                    organizationId, type.Id, request.RelatedSupplierIds!, actorUserId, cancellationToken);

                if (!linksChanged && changes.Count == 0)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<AssistanceTypeDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");
                }

                if (linksChanged && changes.Count == 0)
                {
                    type.Version++;
                    type.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<AssistanceTypeDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<AssistanceTypeDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        var related = await GetRelatedSuppliersAsync(organizationId, type.Id, cancellationToken);
        return ServiceResult<AssistanceTypeDto>.Ok(Map(type, related));
    }

    public async Task<ServiceResult<AssistanceTypeDto>> DeactivateAsync(
        Guid organizationId,
        Guid id,
        DeactivateAssistanceTypeRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<AssistanceTypeDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<AssistanceTypeDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");

        var type = await db.AssistanceTypes.FirstOrDefaultAsync(
            t => t.Id == id && t.OrganizationId == organizationId, cancellationToken);
        if (type is null)
            return ServiceResult<AssistanceTypeDto>.Fail(404, "NOT_FOUND", "סוג הסיוע לא נמצא");

        if (type.Status == "inactive")
            return ServiceResult<AssistanceTypeDto>.Fail(409, "ALREADY_INACTIVE", "סוג הסיוע כבר אינו פעיל");

        if (type.Version != expectedVersion)
            return ServiceResult<AssistanceTypeDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var oldStatus = type.Status;
        type.Status = "inactive";
        type.Version++;
        type.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceTypeDeactivate,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "assistance_type",
                EntityId = type.Id,
                Action = "assistance_type_deactivate",
                FieldName = "status",
                OldValue = oldStatus,
                NewValue = "inactive",
                Reason = reason
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<AssistanceTypeDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<AssistanceTypeDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        var related = await GetRelatedSuppliersAsync(organizationId, type.Id, cancellationToken);
        return ServiceResult<AssistanceTypeDto>.Ok(Map(type, related));
    }

    private async Task<Dictionary<Guid, IReadOnlyList<RelatedSupplierDto>>> LoadRelatedSuppliersByTypeAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from link in db.AssistanceTypeSuppliers
            join supplier in db.Suppliers on link.SupplierId equals supplier.Id
            where link.OrganizationId == organizationId
                  && supplier.OrganizationId == organizationId
                  && supplier.Status == "active"
            orderby supplier.Name
            select new { link.AssistanceTypeId, SupplierId = supplier.Id, supplier.Name }
        ).ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.AssistanceTypeId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<RelatedSupplierDto>)g
                    .Select(r => new RelatedSupplierDto { Id = r.SupplierId, Name = r.Name })
                    .ToList());
    }

    private async Task<IReadOnlyList<RelatedSupplierDto>> GetRelatedSuppliersAsync(
        Guid organizationId,
        Guid assistanceTypeId,
        CancellationToken cancellationToken)
    {
        var byType = await LoadRelatedSuppliersByTypeAsync(organizationId, cancellationToken);
        return byType.GetValueOrDefault(assistanceTypeId, []);
    }

    private async Task<bool> SyncRelatedSuppliersAsync(
        Guid organizationId,
        Guid assistanceTypeId,
        IReadOnlyList<Guid> supplierIds,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var distinctIds = supplierIds.Distinct().ToList();

        if (distinctIds.Count > 0)
        {
            var suppliers = await db.Suppliers
                .Where(s => s.OrganizationId == organizationId && distinctIds.Contains(s.Id))
                .ToListAsync(cancellationToken);

            if (suppliers.Count != distinctIds.Count)
                throw new ArgumentException("ספק לא נמצא בארגון");

            if (suppliers.Any(s => s.Status != "active"))
                throw new ArgumentException("ניתן לקשר רק ספק פעיל");
        }

        var existing = await db.AssistanceTypeSuppliers
            .Where(ats => ats.OrganizationId == organizationId && ats.AssistanceTypeId == assistanceTypeId)
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(e => e.SupplierId).ToHashSet();
        var desiredIds = distinctIds.ToHashSet();
        var toRemove = existing.Where(e => !desiredIds.Contains(e.SupplierId)).ToList();
        var toAdd = distinctIds.Where(id => !existingIds.Contains(id)).ToList();

        if (toRemove.Count == 0 && toAdd.Count == 0)
            return false;

        var now = DateTime.UtcNow;
        foreach (var link in toRemove)
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceTypeRelatedSupplierRemove,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "assistance_type",
                EntityId = assistanceTypeId,
                Action = "related_supplier_remove",
                FieldName = "supplier_id",
                OldValue = link.SupplierId.ToString(),
                NewValue = null
            });
            db.AssistanceTypeSuppliers.Remove(link);
        }

        foreach (var supplierId in toAdd)
        {
            db.AssistanceTypeSuppliers.Add(new AssistanceTypeSupplier
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                AssistanceTypeId = assistanceTypeId,
                SupplierId = supplierId,
                CreatedAt = now
            });
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.AssistanceTypeRelatedSupplierAdd,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "assistance_type",
                EntityId = assistanceTypeId,
                Action = "related_supplier_add",
                FieldName = "supplier_id",
                OldValue = null,
                NewValue = supplierId.ToString()
            });
        }

        return true;
    }

    private static string? ValidateNoDuplicateSupplierIds(IReadOnlyList<Guid> supplierIds)
    {
        if (supplierIds.Count != supplierIds.Distinct().Count())
            return "מזהה ספק כפול בבקשה";
        return null;
    }

    private List<string> ValidateCreateRequest(CreateAssistanceTypeRequest request)
    {
        var errors = new List<string>();

        var code = request.TypeCode?.Trim() ?? string.Empty;
        if (code.Length < 2 || code.Length > 50)
            errors.Add("קוד סוג הסיוע הוא שדה חובה");
        else if (code != code.ToUpperInvariant() || !TypeCodeRegex.IsMatch(code))
            errors.Add("קוד סוג הסיוע חייב להיות באותיות גדולות, ספרות ומקף בלבד");

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length < 2 || name.Length > 200)
            errors.Add("שם סוג הסיוע הוא שדה חובה");

        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 1000)
            errors.Add("תיאור חייב להיות עד 1000 תווים");

        if (request.DefaultAmount is < 0 or > 1000000)
            errors.Add("סכום ברירת מחדל חייב להיות בין 0 ל-1,000,000");

        var freq = request.Frequency?.Trim() ?? string.Empty;
        if (freq.Length > 0 && !AllowedFrequencies.Contains(freq))
            errors.Add("תדירות לא חוקית");

        return errors;
    }

    private static string ResolveCreateFrequency(string? frequency)
    {
        var trimmed = frequency?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? "one_time" : trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static AssistanceTypeDto Map(AssistanceType t, IReadOnlyList<RelatedSupplierDto> related) => new()
    {
        Id = t.Id,
        TypeCode = t.TypeCode,
        Name = t.Name,
        Description = t.Description,
        DefaultAmount = t.DefaultAmount,
        Currency = t.Currency,
        Frequency = t.Frequency,
        Status = t.Status,
        Version = t.Version,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        RelatedSuppliers = related
    };
}
