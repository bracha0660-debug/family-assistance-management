using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class FamilyService(
    AppDbContext db,
    IAuditService auditService)
{
    private const string InvalidIdMessage = "מספר תעודת זהות אינו תקין";

    public async Task<ServiceResult<FamilyListResponse>> ListFamiliesAsync(
        Guid organizationId,
        string viewerRole,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = db.Families
            .Where(f => f.OrganizationId == organizationId);

        if (viewerRole == Roles.Coordinator)
        {
            query = query.Where(f => f.AssignedCoordinatorId == viewerUserId);
        }

        var families = await query
            .OrderBy(f => f.FamilyCode)
            .Join(
                db.Users,
                f => f.AssignedCoordinatorId,
                u => u.Id,
                (f, u) => new FamilyDto
                {
                    Id = f.Id,
                    FamilyCode = f.FamilyCode,
                    HeadOfHouseholdName = f.HeadOfHouseholdName,
                    HeadIdNumber = f.HeadIdNumber,
                    Phone = f.Phone,
                    Address = f.Address,
                    HouseholdSize = f.HouseholdSize,
                    AssignedCoordinatorId = f.AssignedCoordinatorId,
                    AssignedCoordinatorName = u.FullName,
                    Status = f.Status,
                    Notes = f.Notes,
                    Version = f.Version,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var total = families.Count;
        var active = families.Count(f => f.Status == "active");
        var inactive = families.Count(f => f.Status == "inactive");

        return ServiceResult<FamilyListResponse>.Ok(new FamilyListResponse
        {
            Summary = new FamilySummaryDto
            {
                Total = total,
                Active = active,
                Inactive = inactive
            },
            Families = families
        });
    }

    public async Task<ServiceResult<FamilyDto>> CreateFamilyAsync(
        Guid organizationId,
        CreateFamilyRequest request,
        Guid coordinatorUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var coordinator = await db.Users.FirstOrDefaultAsync(
            u => u.Id == coordinatorUserId && u.OrganizationId == organizationId, cancellationToken);
        if (coordinator is null || coordinator.Status != "active" || coordinator.Role != Roles.Coordinator)
            return ServiceResult<FamilyDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var nextCounter = await db.Database
                .SqlQuery<int>(
                    $@"UPDATE organizations SET family_code_counter = family_code_counter + 1 WHERE id = {organizationId} RETURNING family_code_counter AS ""Value""")
                .ToListAsync(cancellationToken);

            if (nextCounter.Count == 0)
                return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");

            var familyCode = $"F-{nextCounter[0]:D6}";
            var now = DateTime.UtcNow;
            var family = new Family
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                FamilyCode = familyCode,
                HeadOfHouseholdName = request.HeadOfHouseholdName.Trim(),
                HeadIdNumber = NormalizeOptional(request.HeadIdNumber),
                Phone = NormalizeOptional(request.Phone),
                Address = NormalizeOptional(request.Address),
                HouseholdSize = request.HouseholdSize ?? 0,
                AssignedCoordinatorId = coordinatorUserId,
                Status = "active",
                Notes = NormalizeOptional(request.Notes),
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Families.Add(family);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.FamilyCreate,
                OrganizationId = organizationId,
                ActorUserId = coordinatorUserId,
                EntityType = "family",
                EntityId = family.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new
                {
                    family.FamilyCode,
                    family.HeadOfHouseholdName,
                    family.HouseholdSize,
                    family.AssignedCoordinatorId
                })
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return ServiceResult<FamilyDto>.Ok(MapFamily(family, coordinator.FullName));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<FamilyDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }
    }

    public async Task<ServiceResult<FamilyDto>> GetFamilyAsync(
        Guid organizationId,
        Guid familyId,
        string viewerRole,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var family = await db.Families
            .FirstOrDefaultAsync(f => f.Id == familyId && f.OrganizationId == organizationId, cancellationToken);
        if (family is null)
            return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "המשפחה לא נמצאה");

        if (viewerRole == Roles.Coordinator && family.AssignedCoordinatorId != viewerUserId)
            return ServiceResult<FamilyDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        var coordinator = await db.Users.FirstOrDefaultAsync(
            u => u.Id == family.AssignedCoordinatorId, cancellationToken);
        return ServiceResult<FamilyDto>.Ok(MapFamily(family, coordinator?.FullName ?? string.Empty));
    }

    public async Task<ServiceResult<FamilyDto>> UpdateFamilyAsync(
        Guid organizationId,
        Guid familyId,
        UpdateFamilyRequest request,
        int? expectedVersion,
        Guid coordinatorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var family = await db.Families.FirstOrDefaultAsync(
            f => f.Id == familyId && f.OrganizationId == organizationId, cancellationToken);
        if (family is null)
            return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "המשפחה לא נמצאה");

        if (family.AssignedCoordinatorId != coordinatorUserId)
            return ServiceResult<FamilyDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (family.Status != "active")
            return ServiceResult<FamilyDto>.Fail(409, "FAMILY_INACTIVE", "המשפחה אינה פעילה");

        if (family.Version != expectedVersion)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var changes = new List<(string Field, string? Old, string? New)>();
        var errors = new List<string>();

        if (request.HeadOfHouseholdName is not null)
        {
            var newName = request.HeadOfHouseholdName.Trim();
            if (newName.Length < 2 || newName.Length > 200)
                errors.Add("שם ראש משק בית הוא שדה חובה");
            else if (newName != family.HeadOfHouseholdName)
            {
                changes.Add(("head_of_household_name", family.HeadOfHouseholdName, newName));
                family.HeadOfHouseholdName = newName;
            }
        }

        if (request.HeadIdNumber is not null)
        {
            var raw = request.HeadIdNumber.Trim();
            string? newId = raw.Length == 0 ? null : raw;
            if (newId is not null && !IsraeliIdValidator.IsValid(newId))
                errors.Add(InvalidIdMessage);
            else if (newId != family.HeadIdNumber)
            {
                changes.Add(("head_id_number", family.HeadIdNumber, newId));
                family.HeadIdNumber = newId;
            }
        }

        if (request.Phone is not null)
        {
            var newPhone = NormalizeOptional(request.Phone);
            if (newPhone is not null && newPhone.Length > 30)
                errors.Add("טלפון חייב להיות עד 30 תווים");
            else if (newPhone != family.Phone)
            {
                changes.Add(("phone", family.Phone, newPhone));
                family.Phone = newPhone;
            }
        }

        if (request.Address is not null)
        {
            var newAddress = NormalizeOptional(request.Address);
            if (newAddress is not null && newAddress.Length > 300)
                errors.Add("כתובת חייבת להיות עד 300 תווים");
            else if (newAddress != family.Address)
            {
                changes.Add(("address", family.Address, newAddress));
                family.Address = newAddress;
            }
        }

        if (request.HouseholdSize is not null)
        {
            var newSize = request.HouseholdSize.Value;
            if (newSize < 0 || newSize > 50)
                errors.Add("גודל משק בית חייב להיות בין 0 ל-50");
            else if (newSize != family.HouseholdSize)
            {
                changes.Add(("household_size", family.HouseholdSize.ToString(), newSize.ToString()));
                family.HouseholdSize = newSize;
            }
        }

        if (request.Notes is not null)
        {
            var newNotes = NormalizeOptional(request.Notes);
            if (newNotes is not null && newNotes.Length > 2000)
                errors.Add("הערות חייבות להיות עד 2000 תווים");
            else if (newNotes != family.Notes)
            {
                changes.Add(("notes", family.Notes, newNotes));
                family.Notes = newNotes;
            }
        }

        if (errors.Count > 0)
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        if (changes.Count == 0)
            return ServiceResult<FamilyDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        family.Version++;
        family.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var change in changes)
            {
                auditService.Stage(new AuditEntry
                {
                    EventCode = BusinessEventCodes.FamilyUpdate,
                    OrganizationId = organizationId,
                    ActorUserId = coordinatorUserId,
                    EntityType = "family",
                    EntityId = family.Id,
                    Action = "update",
                    FieldName = change.Field,
                    OldValue = change.Old,
                    NewValue = change.New
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<FamilyDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        var coordinator = await db.Users.FirstOrDefaultAsync(
            u => u.Id == family.AssignedCoordinatorId, cancellationToken);
        return ServiceResult<FamilyDto>.Ok(MapFamily(family, coordinator?.FullName ?? string.Empty));
    }

    public async Task<ServiceResult<FamilyDto>> DeactivateFamilyAsync(
        Guid organizationId,
        Guid familyId,
        DeactivateFamilyRequest request,
        int? expectedVersion,
        Guid coordinatorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");

        var family = await db.Families.FirstOrDefaultAsync(
            f => f.Id == familyId && f.OrganizationId == organizationId, cancellationToken);
        if (family is null)
            return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "המשפחה לא נמצאה");

        if (family.AssignedCoordinatorId != coordinatorUserId)
            return ServiceResult<FamilyDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (family.Status == "inactive")
            return ServiceResult<FamilyDto>.Fail(409, "ALREADY_INACTIVE", "המשפחה כבר אינה פעילה");

        if (family.Version != expectedVersion)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var oldStatus = family.Status;
        family.Status = "inactive";
        family.Version++;
        family.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.FamilyDeactivate,
                OrganizationId = organizationId,
                ActorUserId = coordinatorUserId,
                EntityType = "family",
                EntityId = family.Id,
                Action = "family_deactivate",
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
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<FamilyDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        var coordinator = await db.Users.FirstOrDefaultAsync(
            u => u.Id == family.AssignedCoordinatorId, cancellationToken);
        return ServiceResult<FamilyDto>.Ok(MapFamily(family, coordinator?.FullName ?? string.Empty));
    }

    private List<string> ValidateCreateRequest(CreateFamilyRequest request)
    {
        var errors = new List<string>();
        var name = request.HeadOfHouseholdName?.Trim() ?? string.Empty;
        if (name.Length < 2 || name.Length > 200)
            errors.Add("שם ראש משק בית הוא שדה חובה");

        if (!string.IsNullOrWhiteSpace(request.HeadIdNumber))
        {
            var trimmed = request.HeadIdNumber.Trim();
            if (!IsraeliIdValidator.IsValid(trimmed))
                errors.Add(InvalidIdMessage);
        }

        if (!string.IsNullOrWhiteSpace(request.Phone) && request.Phone.Trim().Length > 30)
            errors.Add("טלפון חייב להיות עד 30 תווים");

        if (!string.IsNullOrWhiteSpace(request.Address) && request.Address.Trim().Length > 300)
            errors.Add("כתובת חייבת להיות עד 300 תווים");

        if (request.HouseholdSize is < 0 or > 50)
            errors.Add("גודל משק בית חייב להיות בין 0 ל-50");

        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim().Length > 2000)
            errors.Add("הערות חייבות להיות עד 2000 תווים");

        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static FamilyDto MapFamily(Family f, string coordinatorName) => new()
    {
        Id = f.Id,
        FamilyCode = f.FamilyCode,
        HeadOfHouseholdName = f.HeadOfHouseholdName,
        HeadIdNumber = f.HeadIdNumber,
        Phone = f.Phone,
        Address = f.Address,
        HouseholdSize = f.HouseholdSize,
        AssignedCoordinatorId = f.AssignedCoordinatorId,
        AssignedCoordinatorName = coordinatorName,
        Status = f.Status,
        Notes = f.Notes,
        Version = f.Version,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };
}
