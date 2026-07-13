using System.Text.Json;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using FamilyAssistance.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class SupplierService(
    AppDbContext db,
    IAuditService auditService)
{
    private const string MaterialReasonRequiredMessage = "יש לציין סיבה לשינוי מהותי";
    private const string DuplicateRegistrationMessage = "מספר עוסק / ח.פ. כבר קיים אצל ספק פעיל בארגון";
    private const string InactiveRegistrationMessage = "קיים ספק מושבת עם מספר עוסק / ח.פ. זה";

    private enum RegistrationConflictKind
    {
        None,
        ActiveDuplicate,
        InactiveDuplicate
    }

    private sealed record RegistrationConflict(
        RegistrationConflictKind Kind,
        Supplier? InactiveSupplier = null);

    public async Task<SupplierListResponse> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var suppliers = await db.Suppliers
            .Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.SupplierCode)
            .Select(s => Map(s))
            .ToListAsync(cancellationToken);

        return new SupplierListResponse
        {
            Summary = new SupplierSummaryDto
            {
                Total = suppliers.Count,
                Active = suppliers.Count(s => s.Status == "active"),
                Inactive = suppliers.Count(s => s.Status == "inactive")
            },
            Suppliers = suppliers
        };
    }

    public async Task<ServiceResult<SupplierDto>> GetAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);
        if (supplier is null)
            return ServiceResult<SupplierDto>.Fail(404, "NOT_FOUND", "הספק לא נמצא");

        return ServiceResult<SupplierDto>.Ok(Map(supplier));
    }

    public async Task<ServiceResult<SupplierDto>> CreateAsync(
        Guid organizationId,
        CreateSupplierRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCreate(request);
        var errors = ValidateCreate(normalized);
        errors.AddRange(BankFieldValidator.ValidateForSave(
            normalized.BankNumber, normalized.BranchNumber, normalized.AccountNumber, normalized.AccountHolderName));
        if (errors.Count > 0)
            return ServiceResult<SupplierDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        if (!string.IsNullOrEmpty(normalized.RegistrationNumber))
        {
            var conflict = await FindRegistrationConflictAsync(
                organizationId, normalized.RegistrationNumber, null, cancellationToken);
            if (conflict.Kind == RegistrationConflictKind.ActiveDuplicate)
                return ServiceResult<SupplierDto>.Fail(409, "DUPLICATE_REGISTRATION_NUMBER", DuplicateRegistrationMessage);
            if (conflict.Kind == RegistrationConflictKind.InactiveDuplicate && !request.AcknowledgeInactiveDuplicate)
                return InactiveConflictFail(conflict.InactiveSupplier!);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var nextCounter = await db.Database
                .SqlQuery<int>(
                    $@"UPDATE organizations SET supplier_code_counter = supplier_code_counter + 1 WHERE id = {organizationId} RETURNING supplier_code_counter AS ""Value""")
                .ToListAsync(cancellationToken);

            if (nextCounter.Count == 0)
                return ServiceResult<SupplierDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");

            var now = DateTime.UtcNow;
            var supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SupplierCode = $"S-{nextCounter[0]:D6}",
                Name = normalized.Name!,
                RegistrationNumber = normalized.RegistrationNumber,
                Phone = normalized.Phone,
                AccountingCode = normalized.AccountingCode,
                Email = normalized.Email,
                Address = normalized.Address,
                BankNumber = NormalizeBankField(normalized.BankNumber),
                BranchNumber = NormalizeBankField(normalized.BranchNumber),
                AccountNumber = NormalizeBankField(normalized.AccountNumber),
                AccountHolderName = NormalizeBankField(normalized.AccountHolderName),
                Status = "active",
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Suppliers.Add(supplier);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.SupplierCreate,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "supplier",
                EntityId = supplier.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new { supplier.SupplierCode, supplier.Name })
            });

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return ServiceResult<SupplierDto>.Ok(Map(supplier));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<SupplierDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }
    }

    public async Task<ServiceResult<SupplierDto>> UpdateAsync(
        Guid organizationId,
        Guid id,
        UpdateSupplierRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<SupplierDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(
            s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);
        if (supplier is null)
            return ServiceResult<SupplierDto>.Fail(404, "NOT_FOUND", "הספק לא נמצא");

        if (supplier.Status != "active")
            return ServiceResult<SupplierDto>.Fail(409, "SUPPLIER_INACTIVE", "הספק אינו פעיל");

        if (supplier.Version != expectedVersion)
            return ServiceResult<SupplierDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var changes = new List<(string Field, string? Old, string? New, string Action, string EventCode)>();
        var errors = new List<string>();

        if (request.Name is not null)
        {
            var newName = request.Name.Trim();
            if (newName.Length < 2 || newName.Length > 200)
                errors.Add("שם הספק הוא שדה חובה");
            else if (newName != supplier.Name)
            {
                changes.Add(("name", supplier.Name, newName, "update", BusinessEventCodes.SupplierUpdate));
                supplier.Name = newName;
            }
        }

        if (request.RegistrationNumber is not null)
        {
            var registrationError = IsraeliCompanyRegistrationValidator.Validate(request.RegistrationNumber);
            if (registrationError is not null)
                errors.Add(registrationError);
            else
            {
                var newReg = request.RegistrationNumber.Trim();
                if (newReg != supplier.RegistrationNumber)
                {
                    var conflict = await FindRegistrationConflictAsync(
                        organizationId, newReg, supplier.Id, cancellationToken);
                    if (conflict.Kind == RegistrationConflictKind.ActiveDuplicate)
                        return ServiceResult<SupplierDto>.Fail(409, "DUPLICATE_REGISTRATION_NUMBER", DuplicateRegistrationMessage);

                    changes.Add(("registration_number", supplier.RegistrationNumber, newReg,
                        "supplier_identity_change", BusinessEventCodes.SupplierIdentityChange));
                    supplier.RegistrationNumber = newReg;
                }
            }
        }

        if (request.Phone is not null)
        {
            var newPhone = NormalizeOptional(request.Phone);
            var phoneError = IsraeliPhoneValidator.Validate(newPhone);
            if (phoneError is not null)
                errors.Add(phoneError);
            else if (newPhone != supplier.Phone)
            {
                changes.Add(("phone", supplier.Phone, newPhone, "update", BusinessEventCodes.SupplierUpdate));
                supplier.Phone = newPhone;
            }
        }

        if (request.Address is not null)
        {
            var newAddress = NormalizeOptional(request.Address);
            if (newAddress is not null && newAddress.Length > 300)
                errors.Add("כתובת חייבת להיות עד 300 תווים");
            else if (newAddress != supplier.Address)
            {
                changes.Add(("address", supplier.Address, newAddress, "update", BusinessEventCodes.SupplierUpdate));
                supplier.Address = newAddress;
            }
        }

        ApplyAccountingCodeUpdate(request, supplier, errors, changes);
        ApplyEmailUpdate(request, supplier, errors, changes);

        ApplyBankUpdate(request, supplier, errors, changes);

        if (errors.Count > 0)
            return ServiceResult<SupplierDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        if (changes.Count == 0)
            return ServiceResult<SupplierDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");

        var materialChanges = changes.Where(c =>
            c.Action is "supplier_identity_change" or "bank_account_change").ToList();
        if (materialChanges.Count > 0)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length < 3 || reason.Length > 500)
                return ServiceResult<SupplierDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);
        }

        supplier.Version++;
        supplier.UpdatedAt = DateTime.UtcNow;
        var materialReason = request.Reason?.Trim();

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var change in changes)
            {
                auditService.Stage(new AuditEntry
                {
                    EventCode = change.EventCode,
                    OrganizationId = organizationId,
                    ActorUserId = actorUserId,
                    EntityType = "supplier",
                    EntityId = supplier.Id,
                    Action = change.Action,
                    FieldName = change.Field,
                    OldValue = change.Old,
                    NewValue = change.New,
                    Reason = change.Action is "supplier_identity_change" or "bank_account_change" ? materialReason : null
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<SupplierDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<SupplierDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<SupplierDto>.Ok(Map(supplier));
    }

    public async Task<ServiceResult<SupplierDto>> DeactivateAsync(
        Guid organizationId,
        Guid id,
        DeactivateSupplierRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<SupplierDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<SupplierDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(
            s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);
        if (supplier is null)
            return ServiceResult<SupplierDto>.Fail(404, "NOT_FOUND", "הספק לא נמצא");

        if (supplier.Status == "inactive")
            return ServiceResult<SupplierDto>.Fail(409, "ALREADY_INACTIVE", "הספק כבר אינו פעיל");

        if (supplier.Version != expectedVersion)
            return ServiceResult<SupplierDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var oldStatus = supplier.Status;
        supplier.Status = "inactive";
        supplier.Version++;
        supplier.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.SupplierDeactivate,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "supplier",
                EntityId = supplier.Id,
                Action = "supplier_deactivate",
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
            return ServiceResult<SupplierDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<SupplierDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<SupplierDto>.Ok(Map(supplier));
    }

    public async Task<ServiceResult<SupplierDto>> RestoreAsync(
        Guid organizationId,
        Guid id,
        RestoreSupplierRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<SupplierDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<SupplierDto>.Fail(400, "VALIDATION_ERROR", MaterialReasonRequiredMessage);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(
            s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);
        if (supplier is null)
            return ServiceResult<SupplierDto>.Fail(404, "NOT_FOUND", "הספק לא נמצא");

        if (supplier.Status == "active")
            return ServiceResult<SupplierDto>.Fail(409, "ALREADY_ACTIVE", "הספק כבר פעיל");

        if (supplier.Version != expectedVersion)
            return ServiceResult<SupplierDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        if (!string.IsNullOrEmpty(supplier.RegistrationNumber))
        {
            var conflict = await FindRegistrationConflictAsync(
                organizationId, supplier.RegistrationNumber, supplier.Id, cancellationToken);
            if (conflict.Kind == RegistrationConflictKind.ActiveDuplicate)
                return ServiceResult<SupplierDto>.Fail(409, "DUPLICATE_REGISTRATION_NUMBER", DuplicateRegistrationMessage);
        }

        var oldStatus = supplier.Status;
        supplier.Status = "active";
        supplier.Version++;
        supplier.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.SupplierRestore,
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                EntityType = "supplier",
                EntityId = supplier.Id,
                Action = "supplier_restore",
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
            return ServiceResult<SupplierDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<SupplierDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<SupplierDto>.Ok(Map(supplier));
    }

    private static CreateSupplierRequest NormalizeCreate(CreateSupplierRequest request) => new()
    {
        Name = request.Name?.Trim() ?? string.Empty,
        RegistrationNumber = NormalizeOptional(request.RegistrationNumber),
        Phone = NormalizeOptional(request.Phone),
        AccountingCode = NormalizeOptional(request.AccountingCode),
        Email = NormalizeOptional(request.Email),
        Address = NormalizeOptional(request.Address),
        BankNumber = request.BankNumber?.Trim(),
        BranchNumber = request.BranchNumber?.Trim(),
        AccountNumber = request.AccountNumber?.Trim(),
        AccountHolderName = request.AccountHolderName?.Trim(),
        AcknowledgeInactiveDuplicate = request.AcknowledgeInactiveDuplicate
    };

    private async Task<RegistrationConflict> FindRegistrationConflictAsync(
        Guid organizationId,
        string normalizedReg,
        Guid? excludeSupplierId,
        CancellationToken cancellationToken)
    {
        var matches = db.Suppliers.Where(s =>
            s.OrganizationId == organizationId &&
            s.RegistrationNumber == normalizedReg);

        if (excludeSupplierId is not null)
            matches = matches.Where(s => s.Id != excludeSupplierId.Value);

        if (await matches.AnyAsync(s => s.Status == "active", cancellationToken))
            return new RegistrationConflict(RegistrationConflictKind.ActiveDuplicate);

        var inactive = await matches
            .Where(s => s.Status == "inactive")
            .OrderByDescending(s => s.UpdatedAt)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (inactive is not null)
            return new RegistrationConflict(RegistrationConflictKind.InactiveDuplicate, inactive);

        return new RegistrationConflict(RegistrationConflictKind.None);
    }

    private static ServiceResult<SupplierDto> InactiveConflictFail(Supplier inactive) =>
        ServiceResult<SupplierDto>.FailWithStructuredDetails(
            409,
            "INACTIVE_SUPPLIER_SAME_REGISTRATION",
            InactiveRegistrationMessage,
            new InactiveSupplierConflictDetails
            {
                ExistingSupplierId = inactive.Id,
                ExistingSupplierCode = inactive.SupplierCode,
                ExistingSupplierName = inactive.Name,
                ExistingVersion = inactive.Version
            });

    private static string? NormalizeBankField(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static List<string> ValidateCreate(CreateSupplierRequest request)
    {
        var errors = new List<string>();
        if (request.Name!.Length < 2 || request.Name.Length > 200)
            errors.Add("שם הספק הוא שדה חובה");

        var registrationError = IsraeliCompanyRegistrationValidator.Validate(request.RegistrationNumber);
        if (registrationError is not null)
            errors.Add(registrationError);

        var phoneError = IsraeliPhoneValidator.Validate(request.Phone);
        if (phoneError is not null)
            errors.Add(phoneError);

        var accountingError = ValidateAccountingCodeRequired(request.AccountingCode);
        if (accountingError is not null)
            errors.Add(accountingError);

        var emailError = EmailValidator.Validate(request.Email);
        if (emailError is not null)
            errors.Add(emailError);

        if (request.Address is not null && request.Address.Length > 300)
            errors.Add("כתובת חייבת להיות עד 300 תווים");
        return errors;
    }

    private static void ApplyAccountingCodeUpdate(
        UpdateSupplierRequest request,
        Supplier supplier,
        List<string> errors,
        List<(string Field, string? Old, string? New, string Action, string EventCode)> changes)
    {
        var storedEmpty = string.IsNullOrWhiteSpace(supplier.AccountingCode);

        if (request.AccountingCode is not null)
        {
            var accountingError = ValidateAccountingCodeRequired(request.AccountingCode);
            if (accountingError is not null)
            {
                errors.Add(accountingError);
                return;
            }

            var newCode = request.AccountingCode.Trim();
            if (newCode != supplier.AccountingCode)
            {
                changes.Add(("accounting_code", supplier.AccountingCode, newCode, "update", BusinessEventCodes.SupplierUpdate));
                supplier.AccountingCode = newCode;
            }
        }
        else if (storedEmpty)
            errors.Add(AccountingCodeRequiredMessage);
    }

    private static void ApplyEmailUpdate(
        UpdateSupplierRequest request,
        Supplier supplier,
        List<string> errors,
        List<(string Field, string? Old, string? New, string Action, string EventCode)> changes)
    {
        if (request.Email is null) return;

        var newEmail = NormalizeOptional(request.Email);
        var emailError = EmailValidator.Validate(newEmail);
        if (emailError is not null)
        {
            errors.Add(emailError);
            return;
        }

        if (newEmail != supplier.Email)
        {
            changes.Add(("email", supplier.Email, newEmail, "update", BusinessEventCodes.SupplierUpdate));
            supplier.Email = newEmail;
        }
    }

    private const string AccountingCodeRequiredMessage = "קוד בהנהלת חשבונות הוא שדה חובה";
    private const string AccountingCodeMaxLengthMessage = "קוד בהנהלת חשבונות חייב להיות עד 50 תווים";

    private static string? ValidateAccountingCodeRequired(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return AccountingCodeRequiredMessage;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return AccountingCodeRequiredMessage;

        if (trimmed.Length > 50)
            return AccountingCodeMaxLengthMessage;

        return null;
    }

    private static void ApplyBankUpdate(
        UpdateSupplierRequest request,
        Supplier supplier,
        List<string> errors,
        List<(string Field, string? Old, string? New, string Action, string EventCode)> changes)
    {
        var hasBankRequest = request.BankNumber is not null
            || request.BranchNumber is not null
            || request.AccountNumber is not null
            || request.AccountHolderName is not null;
        if (!hasBankRequest) return;

        var mergedBank = request.BankNumber is not null ? NormalizeBankField(request.BankNumber) : supplier.BankNumber;
        var mergedBranch = request.BranchNumber is not null ? NormalizeBankField(request.BranchNumber) : supplier.BranchNumber;
        var mergedAccount = request.AccountNumber is not null ? NormalizeBankField(request.AccountNumber) : supplier.AccountNumber;
        var mergedHolder = request.AccountHolderName is not null ? NormalizeBankField(request.AccountHolderName) : supplier.AccountHolderName;

        errors.AddRange(BankFieldValidator.ValidateForSave(mergedBank, mergedBranch, mergedAccount, mergedHolder));
        if (errors.Count > 0) return;

        ApplyBankFieldChange(mergedBank, supplier.BankNumber, "bank_number", changes, v => supplier.BankNumber = v);
        ApplyBankFieldChange(mergedBranch, supplier.BranchNumber, "branch_number", changes, v => supplier.BranchNumber = v);
        ApplyBankFieldChange(mergedAccount, supplier.AccountNumber, "account_number", changes, v => supplier.AccountNumber = v);
        ApplyBankFieldChange(mergedHolder, supplier.AccountHolderName, "account_holder_name", changes, v => supplier.AccountHolderName = v);
    }

    private static void ApplyBankFieldChange(
        string? newValue,
        string? current,
        string fieldName,
        List<(string Field, string? Old, string? New, string Action, string EventCode)> changes,
        Action<string?> apply)
    {
        if (newValue == current) return;
        changes.Add((fieldName, current, newValue, "bank_account_change", BusinessEventCodes.SupplierUpdate));
        apply(newValue);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static SupplierDto Map(Supplier s) => new()
    {
        Id = s.Id,
        SupplierCode = s.SupplierCode,
        Name = s.Name,
        RegistrationNumber = s.RegistrationNumber,
        Phone = s.Phone,
        AccountingCode = s.AccountingCode,
        Email = s.Email,
        Address = s.Address,
        BankNumber = s.BankNumber,
        BranchNumber = s.BranchNumber,
        AccountNumber = s.AccountNumber,
        AccountHolderName = s.AccountHolderName,
        Status = s.Status,
        Version = s.Version,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}
