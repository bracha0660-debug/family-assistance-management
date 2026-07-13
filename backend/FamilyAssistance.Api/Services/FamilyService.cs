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
    private const string DuplicateIdMessage = "מספר תעודת זהות כבר קיים בארגון";
    private const string LastNameRequiredMessage = "שם משפחה הוא שדה חובה";
    private const string DuplicateAccountingMessage = "מספר חשבונאי כבר קיים לרכז זה";
    private const string MaterialReasonRequiredMessage = "יש לציין סיבה לשינוי מהותי";

    private static readonly HashSet<string> MaterialFields =
    [
        "accounting_code",
        "assigned_coordinator_id",
        "father_israeli_id",
        "mother_israeli_id",
        "bank_number",
        "branch_number",
        "account_number",
        "account_holder_name"
    ];

    public async Task<ServiceResult<FamilyListResponse>> ListFamiliesAsync(
        Guid organizationId,
        AuthorizationContext auth,
        string? ownership = null,
        CancellationToken cancellationToken = default)
    {
        var query = ScopeEvaluator.ApplyFamilyListScope(
            db.Families.Where(f => f.OrganizationId == organizationId),
            auth,
            PermissionKeys.FamiliesView);

        if (ownership == "mine")
            query = WorkflowHelpers.ApplyOwnershipMine(query, auth.UserId);

        var familyRows = await query
            .OrderBy(f => f.FamilyCode)
            .ToListAsync(cancellationToken);

        var coordinatorIds = familyRows
            .SelectMany(f => new[] { f.AssignedCoordinatorId, f.AccountingCoordinatorId })
            .Distinct()
            .ToList();
        var coordinators = await db.Users
            .Where(u => coordinatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var families = familyRows
            .Select(f => MapFamily(f, coordinators.GetValueOrDefault(f.AssignedCoordinatorId, string.Empty)))
            .ToList();

        return ServiceResult<FamilyListResponse>.Ok(new FamilyListResponse
        {
            Summary = new FamilySummaryDto
            {
                Total = families.Count,
                Active = families.Count(f => f.Status == "active"),
                Inactive = families.Count(f => f.Status == "inactive")
            },
            Families = families
        });
    }

    public async Task<ServiceResult<SuggestedAccountingCodeResponse>> GetSuggestedAccountingCodeAsync(
        Guid organizationId,
        Guid accountingCoordinatorId,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var coordinator = await db.Users.FirstOrDefaultAsync(
            u => u.Id == accountingCoordinatorId && u.OrganizationId == organizationId && u.Status == "active",
            cancellationToken);
        if (coordinator is null)
            return ServiceResult<SuggestedAccountingCodeResponse>.Fail(404, "NOT_FOUND", "הרכז לא נמצא");

        if (!auth.FullOrgAccess && auth.UserId != accountingCoordinatorId
            && !ScopeEvaluator.CanAccessFamily(auth, new Family { AssignedCoordinatorId = accountingCoordinatorId }, PermissionKeys.FamiliesCreate))
        {
            return ServiceResult<SuggestedAccountingCodeResponse>.Fail(403, "FORBIDDEN", "אין הרשאה");
        }

        var maxCode = await db.Families
            .Where(f => f.OrganizationId == organizationId && f.AccountingCoordinatorId == accountingCoordinatorId)
            .Select(f => (long?)f.AccountingCode)
            .MaxAsync(cancellationToken) ?? 0;

        return ServiceResult<SuggestedAccountingCodeResponse>.Ok(new SuggestedAccountingCodeResponse
        {
            AccountingCoordinatorId = accountingCoordinatorId,
            SuggestedAccountingCode = maxCode + 1
        });
    }

    public async Task<ServiceResult<FamilyDto>> CreateFamilyAsync(
        Guid organizationId,
        CreateFamilyRequest request,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCreateRequest(request);
        var errors = ValidateCreateRequest(normalized);
        errors.AddRange(BankFieldValidator.ValidateForSave(
            normalized.BankNumber, normalized.BranchNumber, normalized.AccountNumber, normalized.AccountHolderName));
        if (errors.Count > 0)
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var assignedCoordinatorId = normalized.AssignedCoordinatorId ?? auth.UserId;
        var coordinator = await db.Users.FirstOrDefaultAsync(
            u => u.Id == assignedCoordinatorId && u.OrganizationId == organizationId && u.Status == "active",
            cancellationToken);
        if (coordinator is null)
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", "רכז לא חוקי");

        if (normalized.AccountingCode is not null && normalized.AccountingCode <= 0)
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", "מספר חשבונאי חייב להיות מספר חיובי");

        var accountingCode = normalized.AccountingCode
            ?? await NextAccountingCodeAsync(organizationId, assignedCoordinatorId, cancellationToken);

        if (await AccountingCodeExistsAsync(
                organizationId, assignedCoordinatorId, accountingCode, null, cancellationToken))
        {
            return ServiceResult<FamilyDto>.Fail(409, "DUPLICATE_ACCOUNTING_CODE", DuplicateAccountingMessage);
        }

        var idErrors = await ValidateIsraeliIdsUniqueAsync(
            organizationId, normalized.FatherIsraeliId, normalized.MotherIsraeliId, null, cancellationToken);
        if (idErrors.Count > 0)
            return ServiceResult<FamilyDto>.Fail(409, "DUPLICATE_ISRAELI_ID", idErrors[0], idErrors);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var nextCodeCounter = await db.Database
                .SqlQuery<int>(
                    $@"UPDATE organizations SET family_code_counter = family_code_counter + 1 WHERE id = {organizationId} RETURNING family_code_counter AS ""Value""")
                .ToListAsync(cancellationToken);

            if (nextCodeCounter.Count == 0)
                return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");

            var now = DateTime.UtcNow;
            var family = new Family
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                FamilyCode = $"F-{nextCodeCounter[0]:D6}",
                AccountingCode = accountingCode,
                AccountingCoordinatorId = assignedCoordinatorId,
                FamilyLastName = normalized.FamilyLastName!,
                FatherName = normalized.FatherName,
                FatherIsraeliId = normalized.FatherIsraeliId,
                MotherName = normalized.MotherName,
                MotherIsraeliId = normalized.MotherIsraeliId,
                Phone = normalized.Phone,
                Address = normalized.Address,
                BankNumber = NormalizeBankField(normalized.BankNumber),
                BranchNumber = NormalizeBankField(normalized.BranchNumber),
                AccountNumber = NormalizeBankField(normalized.AccountNumber),
                AccountHolderName = NormalizeBankField(normalized.AccountHolderName),
                AssignedCoordinatorId = assignedCoordinatorId,
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Families.Add(family);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.FamilyCreate,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "family",
                EntityId = family.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new
                {
                    family.FamilyCode,
                    family.AccountingCode,
                    family.AccountingCoordinatorId,
                    family.FamilyLastName,
                    family.AssignedCoordinatorId
                })
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return ServiceResult<FamilyDto>.Ok(MapFamily(family, coordinator.FullName));
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<FamilyDto>.Fail(409, "DUPLICATE_ACCOUNTING_CODE", DuplicateAccountingMessage);
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
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        var family = await db.Families
            .FirstOrDefaultAsync(f => f.Id == familyId && f.OrganizationId == organizationId, cancellationToken);
        if (family is null)
            return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "המשפחה לא נמצאה");

        if (!ScopeEvaluator.CanAccessFamily(auth, family, PermissionKeys.FamiliesView))
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
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var family = await db.Families.FirstOrDefaultAsync(
            f => f.Id == familyId && f.OrganizationId == organizationId, cancellationToken);
        if (family is null)
            return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "המשפחה לא נמצאה");

        if (!ScopeEvaluator.CanAccessFamily(auth, family, PermissionKeys.FamiliesEdit))
            return ServiceResult<FamilyDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (family.Status != "active")
            return ServiceResult<FamilyDto>.Fail(409, "FAMILY_INACTIVE", "המשפחה אינה פעילה");

        if (family.Version != expectedVersion)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var changes = new List<(string Field, string? Old, string? New, string Action)>();
        var errors = new List<string>();

        if (request.FamilyLastName is not null)
        {
            var newName = request.FamilyLastName.Trim();
            if (newName.Length == 0 || newName.Length < 2 || newName.Length > 200)
                errors.Add(LastNameRequiredMessage);
            else if (newName != family.FamilyLastName)
            {
                changes.Add(("family_last_name", family.FamilyLastName, newName, "update"));
                family.FamilyLastName = newName;
            }
        }

        if (request.AccountingCode is not null)
        {
            var newCode = request.AccountingCode.Value;
            if (newCode <= 0)
                errors.Add("מספר חשבונאי חייב להיות מספר חיובי");
            else if (newCode != family.AccountingCode)
            {
                if (await AccountingCodeExistsAsync(
                        organizationId, family.AccountingCoordinatorId, newCode, family.Id, cancellationToken))
                    errors.Add(DuplicateAccountingMessage);
                else
                {
                    changes.Add(("accounting_code", family.AccountingCode.ToString(), newCode.ToString(), "accounting_code_change"));
                    family.AccountingCode = newCode;
                }
            }
        }

        ApplyOptionalNameChange(request.FatherName, family.FatherName, "father_name", errors, changes,
            v => family.FatherName = v);
        ApplyOptionalIdChange(request.FatherIsraeliId, family.FatherIsraeliId, "father_israeli_id", errors, changes,
            v => family.FatherIsraeliId = v);
        ApplyOptionalNameChange(request.MotherName, family.MotherName, "mother_name", errors, changes,
            v => family.MotherName = v);
        ApplyOptionalIdChange(request.MotherIsraeliId, family.MotherIsraeliId, "mother_israeli_id", errors, changes,
            v => family.MotherIsraeliId = v);

        if (request.Phone is not null)
        {
            var newPhone = NormalizeOptional(request.Phone);
            if (newPhone is not null && newPhone.Length > 30)
                errors.Add("טלפון חייב להיות עד 30 תווים");
            else if (newPhone != family.Phone)
            {
                changes.Add(("phone", family.Phone, newPhone, "update"));
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
                changes.Add(("address", family.Address, newAddress, "update"));
                family.Address = newAddress;
            }
        }

        if (request.AssignedCoordinatorId is not null && request.AssignedCoordinatorId != family.AssignedCoordinatorId)
        {
            var newCoordinatorId = request.AssignedCoordinatorId.Value;
            var newCoordinator = await db.Users.FirstOrDefaultAsync(
                u => u.Id == newCoordinatorId && u.OrganizationId == organizationId && u.Status == "active",
                cancellationToken);
            if (newCoordinator is null)
                errors.Add("רכז לא חוקי");
            else
            {
                changes.Add(("assigned_coordinator_id",
                    family.AssignedCoordinatorId.ToString(),
                    newCoordinatorId.ToString(),
                    "assigned_coordinator_change"));
                family.AssignedCoordinatorId = newCoordinatorId;
            }
        }

        ApplyBankChanges(request, family, errors, changes);

        if (errors.Count > 0)
        {
            var code = errors.Contains(DuplicateAccountingMessage) ? "DUPLICATE_ACCOUNTING_CODE"
                : errors.Contains(DuplicateIdMessage) ? "DUPLICATE_ISRAELI_ID"
                : "VALIDATION_ERROR";
            var status = code.StartsWith("DUPLICATE", StringComparison.Ordinal) ? 409 : 400;
            return ServiceResult<FamilyDto>.Fail(status, code, errors[0], errors);
        }

        if (changes.Count == 0)
            return ServiceResult<FamilyDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        var materialChanges = changes.Where(c => MaterialFields.Contains(c.Field)).ToList();
        if (materialChanges.Count > 0)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length < 3 || reason.Length > 500)
                return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

            var idErrors = await ValidateIsraeliIdsUniqueAsync(
                organizationId, family.FatherIsraeliId, family.MotherIsraeliId, family.Id, cancellationToken);
            if (idErrors.Count > 0)
                return ServiceResult<FamilyDto>.Fail(409, "DUPLICATE_ISRAELI_ID", idErrors[0], idErrors);
        }

        family.Version++;
        family.UpdatedAt = DateTime.UtcNow;
        var materialReason = request.Reason?.Trim();

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var change in changes)
            {
                var eventCode = change.Field is "bank_number" or "branch_number" or "account_number" or "account_holder_name"
                    ? BusinessEventCodes.FamilyBankChange
                    : BusinessEventCodes.FamilyUpdate;

                auditService.Stage(new AuditEntry
                {
                    EventCode = eventCode,
                    OrganizationId = organizationId,
                    ActorUserId = auth.UserId,
                    EntityType = "family",
                    EntityId = family.Id,
                    Action = change.Action,
                    FieldName = change.Field,
                    OldValue = change.Old,
                    NewValue = change.New,
                    Reason = MaterialFields.Contains(change.Field) ? materialReason : null
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
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<FamilyDto>.Fail(409, "DUPLICATE_ACCOUNTING_CODE", DuplicateAccountingMessage);
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
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var family = await db.Families.FirstOrDefaultAsync(
            f => f.Id == familyId && f.OrganizationId == organizationId, cancellationToken);
        if (family is null)
            return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "המשפחה לא נמצאה");

        if (!ScopeEvaluator.CanAccessFamily(auth, family, PermissionKeys.FamiliesDeactivate))
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
                ActorUserId = auth.UserId,
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

    public async Task<ServiceResult<FamilyDto>> RestoreFamilyAsync(
        Guid organizationId,
        Guid familyId,
        RestoreFamilyRequest request,
        int? expectedVersion,
        AuthorizationContext auth,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<FamilyDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var family = await db.Families.FirstOrDefaultAsync(
            f => f.Id == familyId && f.OrganizationId == organizationId, cancellationToken);
        if (family is null)
            return ServiceResult<FamilyDto>.Fail(404, "NOT_FOUND", "המשפחה לא נמצאה");

        if (!ScopeEvaluator.CanAccessFamily(auth, family, PermissionKeys.FamiliesRestore))
            return ServiceResult<FamilyDto>.Fail(403, "FORBIDDEN", "אין הרשאה");

        if (family.Status == "active")
            return ServiceResult<FamilyDto>.Fail(409, "ALREADY_ACTIVE", "המשפחה כבר פעילה");

        if (family.Version != expectedVersion)
            return ServiceResult<FamilyDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var oldStatus = family.Status;
        family.Status = "active";
        family.Version++;
        family.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.FamilyRestore,
                OrganizationId = organizationId,
                ActorUserId = auth.UserId,
                EntityType = "family",
                EntityId = family.Id,
                Action = "family_restore",
                FieldName = "status",
                OldValue = oldStatus,
                NewValue = "active",
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

    private async Task<long> NextAccountingCodeAsync(
        Guid organizationId,
        Guid accountingCoordinatorId,
        CancellationToken cancellationToken)
    {
        var maxCode = await db.Families
            .Where(f => f.OrganizationId == organizationId && f.AccountingCoordinatorId == accountingCoordinatorId)
            .Select(f => (long?)f.AccountingCode)
            .MaxAsync(cancellationToken) ?? 0;
        return maxCode + 1;
    }

    private async Task<bool> AccountingCodeExistsAsync(
        Guid organizationId,
        Guid accountingCoordinatorId,
        long accountingCode,
        Guid? excludeFamilyId,
        CancellationToken cancellationToken)
    {
        var query = db.Families.Where(f =>
            f.OrganizationId == organizationId
            && f.AccountingCoordinatorId == accountingCoordinatorId
            && f.AccountingCode == accountingCode);
        if (excludeFamilyId is not null)
            query = query.Where(f => f.Id != excludeFamilyId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    private async Task<List<string>> ValidateIsraeliIdsUniqueAsync(
        Guid organizationId,
        string? fatherId,
        string? motherId,
        Guid? excludeFamilyId,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(fatherId)) ids.Add(fatherId);
        if (!string.IsNullOrWhiteSpace(motherId)) ids.Add(motherId);

        foreach (var id in ids)
        {
            var query = db.Families.Where(f =>
                f.OrganizationId == organizationId
                && (f.FatherIsraeliId == id || f.MotherIsraeliId == id));
            if (excludeFamilyId is not null)
                query = query.Where(f => f.Id != excludeFamilyId.Value);
            if (await query.AnyAsync(cancellationToken))
                errors.Add(DuplicateIdMessage);
        }

        return errors;
    }

    private static CreateFamilyRequest NormalizeCreateRequest(CreateFamilyRequest request)
    {
        var fatherId = IsraeliIdNormalizer.Normalize(request.FatherIsraeliId);
        if (request.FatherIsraeliId is not null && fatherId is null)
            fatherId = request.FatherIsraeliId.Trim();

        var motherId = IsraeliIdNormalizer.Normalize(request.MotherIsraeliId);
        if (request.MotherIsraeliId is not null && motherId is null)
            motherId = request.MotherIsraeliId.Trim();

        return new CreateFamilyRequest
        {
            FamilyLastName = request.FamilyLastName?.Trim() ?? string.Empty,
            AccountingCode = request.AccountingCode,
            AssignedCoordinatorId = request.AssignedCoordinatorId,
            FatherName = NormalizeOptional(request.FatherName),
            FatherIsraeliId = fatherId,
            MotherName = NormalizeOptional(request.MotherName),
            MotherIsraeliId = motherId,
            Phone = NormalizeOptional(request.Phone),
            Address = NormalizeOptional(request.Address),
            BankNumber = request.BankNumber?.Trim(),
            BranchNumber = request.BranchNumber?.Trim(),
            AccountNumber = request.AccountNumber?.Trim(),
            AccountHolderName = request.AccountHolderName?.Trim()
        };
    }

    private static string? NormalizeBankField(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static List<string> ValidateCreateRequest(CreateFamilyRequest request)
    {
        var errors = new List<string>();
        if (request.FamilyLastName!.Length == 0 || request.FamilyLastName.Length < 2 || request.FamilyLastName.Length > 200)
            errors.Add(LastNameRequiredMessage);

        ValidateOptionalId(request.FatherIsraeliId, errors);
        ValidateOptionalId(request.MotherIsraeliId, errors);

        if (!string.IsNullOrWhiteSpace(request.FatherName) && request.FatherName.Length > 200)
            errors.Add("שם האב חייב להיות עד 200 תווים");
        if (!string.IsNullOrWhiteSpace(request.MotherName) && request.MotherName.Length > 200)
            errors.Add("שם האם חייב להיות עד 200 תווים");

        if (request.Phone is not null && request.Phone.Length > 30)
            errors.Add("טלפון חייב להיות עד 30 תווים");
        if (request.Address is not null && request.Address.Length > 300)
            errors.Add("כתובת חייבת להיות עד 300 תווים");

        return errors;
    }

    private static void ValidateOptionalId(string? id, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (!IsraeliIdValidator.IsValid(id))
            errors.Add(InvalidIdMessage);
    }

    private static void ApplyOptionalNameChange(
        string? incoming,
        string? current,
        string fieldName,
        List<string> errors,
        List<(string Field, string? Old, string? New, string Action)> changes,
        Action<string?> apply)
    {
        if (incoming is null) return;
        var newValue = NormalizeOptional(incoming);
        if (newValue is not null && newValue.Length > 200)
        {
            errors.Add(fieldName == "father_name" ? "שם האב חייב להיות עד 200 תווים" : "שם האם חייב להיות עד 200 תווים");
            return;
        }

        if (newValue != current)
        {
            changes.Add((fieldName, current, newValue, "update"));
            apply(newValue);
        }
    }

    private static void ApplyOptionalIdChange(
        string? incoming,
        string? current,
        string fieldName,
        List<string> errors,
        List<(string Field, string? Old, string? New, string Action)> changes,
        Action<string?> apply)
    {
        if (incoming is null) return;
        var newValue = IsraeliIdNormalizer.Normalize(incoming) ?? NormalizeOptional(incoming);
        if (newValue is not null && !IsraeliIdValidator.IsValid(newValue))
        {
            errors.Add(InvalidIdMessage);
            return;
        }

        if (newValue != current)
        {
            changes.Add((fieldName, current, newValue, "identity_change"));
            apply(newValue);
        }
    }

    private static void ApplyBankChanges(
        UpdateFamilyRequest request,
        Family family,
        List<string> errors,
        List<(string Field, string? Old, string? New, string Action)> changes)
    {
        var hasBankRequest = request.BankNumber is not null
            || request.BranchNumber is not null
            || request.AccountNumber is not null
            || request.AccountHolderName is not null;
        if (!hasBankRequest) return;

        var mergedBank = request.BankNumber is not null ? NormalizeBankField(request.BankNumber) : family.BankNumber;
        var mergedBranch = request.BranchNumber is not null ? NormalizeBankField(request.BranchNumber) : family.BranchNumber;
        var mergedAccount = request.AccountNumber is not null ? NormalizeBankField(request.AccountNumber) : family.AccountNumber;
        var mergedHolder = request.AccountHolderName is not null ? NormalizeBankField(request.AccountHolderName) : family.AccountHolderName;

        errors.AddRange(BankFieldValidator.ValidateForSave(mergedBank, mergedBranch, mergedAccount, mergedHolder));
        if (errors.Count > 0) return;

        ApplyBankFieldChange(mergedBank, family.BankNumber, "bank_number", changes, v => family.BankNumber = v);
        ApplyBankFieldChange(mergedBranch, family.BranchNumber, "branch_number", changes, v => family.BranchNumber = v);
        ApplyBankFieldChange(mergedAccount, family.AccountNumber, "account_number", changes, v => family.AccountNumber = v);
        ApplyBankFieldChange(mergedHolder, family.AccountHolderName, "account_holder_name", changes, v => family.AccountHolderName = v);
    }

    private static void ApplyBankFieldChange(
        string? newValue,
        string? current,
        string fieldName,
        List<(string Field, string? Old, string? New, string Action)> changes,
        Action<string?> apply)
    {
        if (newValue == current) return;
        changes.Add((fieldName, current, newValue, "bank_account_change"));
        apply(newValue);
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
        AccountingCode = f.AccountingCode,
        AccountingCoordinatorId = f.AccountingCoordinatorId,
        FamilyLastName = f.FamilyLastName,
        FatherName = f.FatherName,
        FatherIsraeliId = f.FatherIsraeliId,
        MotherName = f.MotherName,
        MotherIsraeliId = f.MotherIsraeliId,
        Phone = f.Phone,
        Address = f.Address,
        BankNumber = f.BankNumber,
        BranchNumber = f.BranchNumber,
        AccountNumber = f.AccountNumber,
        AccountHolderName = f.AccountHolderName,
        AssignedCoordinatorId = f.AssignedCoordinatorId,
        AssignedCoordinatorName = coordinatorName,
        Status = f.Status,
        Version = f.Version,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };
}
