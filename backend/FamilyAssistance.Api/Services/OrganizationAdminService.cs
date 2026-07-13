using System.Text.Json;
using System.Text.RegularExpressions;
using FamilyAssistance.Api.Audit;
using FamilyAssistance.Api.Auth;
using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistance.Api.Services;

public sealed class OrganizationAdminService(
    AppDbContext db,
    IAuditService auditService,
    SessionService sessionService,
    PermissionService permissionService)
{
    private static readonly Regex OrgCodeRegex = new("^[A-Z0-9-]{2,50}$", RegexOptions.CultureInvariant);

    public async Task<OrganizationListResponse> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await db.Organizations
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                Code = o.Code,
                Status = o.Status,
                Version = o.Version,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                HasOrgAdmin = db.Users.Any(u =>
                    u.OrganizationId == o.Id && u.Role == Roles.OrganizationAdministrator)
            })
            .ToListAsync(cancellationToken);

        var total = organizations.Count;
        var active = organizations.Count(o => o.Status == "active");
        var suspended = organizations.Count(o => o.Status == "suspended");

        return new OrganizationListResponse
        {
            Summary = new OrganizationSummaryDto
            {
                Total = total,
                Active = active,
                Suspended = suspended
            },
            Organizations = organizations
        };
    }

    public async Task<ServiceResult<OrganizationDto>> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
            return ServiceResult<OrganizationDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var code = request.Code.Trim();
        if (await db.Organizations.AnyAsync(o => o.Code == code, cancellationToken))
            return ServiceResult<OrganizationDto>.Fail(409, "DUPLICATE_ORG_CODE", "קוד הארגון כבר קיים");

        var now = DateTime.UtcNow;
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = code,
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Organizations.Add(org);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.OrganizationCreate,
                OrganizationId = null,
                ActorUserId = actorUserId,
                EntityType = "organization",
                EntityId = org.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new
                {
                    org.Name,
                    org.Code,
                    org.Status
                })
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        await permissionService.SeedOrganizationRolesAsync(org.Id, actorUserId, cancellationToken);

        return ServiceResult<OrganizationDto>.Ok(MapOrganization(org, hasOrgAdmin: false));
    }

    public async Task<ServiceResult<OrganizationDto>> SuspendOrganizationAsync(
        Guid organizationId,
        SuspendOrganizationRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<OrganizationDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<OrganizationDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");

        var org = await db.Organizations.FindAsync([organizationId], cancellationToken);
        if (org is null)
            return ServiceResult<OrganizationDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");

        if (org.Status == "suspended")
            return ServiceResult<OrganizationDto>.Fail(409, "ALREADY_SUSPENDED", "הארגון כבר מושעה");

        if (org.Version != expectedVersion)
            return ServiceResult<OrganizationDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var oldStatus = org.Status;
        org.Status = "suspended";
        org.Version++;
        org.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.OrganizationSuspend,
                OrganizationId = null,
                ActorUserId = actorUserId,
                EntityType = "organization",
                EntityId = org.Id,
                Action = "status_change",
                FieldName = "status",
                OldValue = oldStatus,
                NewValue = "suspended",
                Reason = reason
            });
            await db.SaveChangesAsync(cancellationToken);
            await sessionService.RevokeOrganizationSessionsAsync(organizationId, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        var hasOrgAdmin = await db.Users.AnyAsync(
            u => u.OrganizationId == org.Id && u.Role == Roles.OrganizationAdministrator,
            cancellationToken);

        return ServiceResult<OrganizationDto>.Ok(MapOrganization(org, hasOrgAdmin));
    }

    public async Task<ServiceResult<BootstrapUserDto>> BootstrapOrgAdminAsync(
        Guid organizationId,
        BootstrapOrgAdminRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateBootstrapRequest(request);
        if (errors.Count > 0)
            return ServiceResult<BootstrapUserDto>.Fail(400, "VALIDATION_ERROR", errors[0], errors);

        var org = await db.Organizations.FindAsync([organizationId], cancellationToken);
        if (org is null)
            return ServiceResult<BootstrapUserDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");

        if (org.Status != "active")
            return ServiceResult<BootstrapUserDto>.Fail(409, "ORG_SUSPENDED", "הארגון אינו פעיל");

        if (await db.Users.AnyAsync(
                u => u.OrganizationId == organizationId && u.Role == Roles.OrganizationAdministrator,
                cancellationToken))
            return ServiceResult<BootstrapUserDto>.Fail(409, "ORG_ADMIN_EXISTS", "קיים כבר מנהל ארגון");

        var username = request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            return ServiceResult<BootstrapUserDto>.Fail(409, "DUPLICATE_USERNAME", "שם משתמש כבר קיים");

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Username = username,
            FullName = request.FullName.Trim(),
            Role = Roles.OrganizationAdministrator,
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Users.Add(user);
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.OrgAdminBootstrap,
                OrganizationId = null,
                ActorUserId = actorUserId,
                EntityType = "user",
                EntityId = user.Id,
                Action = "create",
                NewValue = JsonSerializer.Serialize(new
                {
                    user.Username,
                    user.Role,
                    user.OrganizationId
                })
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<BootstrapUserDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        return ServiceResult<BootstrapUserDto>.Ok(new BootstrapUserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            OrganizationId = user.OrganizationId!.Value,
            Status = user.Status
        });
    }

    public async Task<ServiceResult<OrganizationDto>> RestoreOrganizationAsync(
        Guid organizationId,
        RestoreOrganizationRequest request,
        int? expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null)
            return ServiceResult<OrganizationDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 3 || reason.Length > 500)
            return ServiceResult<OrganizationDto>.Fail(400, "VALIDATION_ERROR", "יש לציין סיבה לשינוי מהותי");

        var org = await db.Organizations.FindAsync([organizationId], cancellationToken);
        if (org is null)
            return ServiceResult<OrganizationDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");
        if (org.Status == "active")
            return ServiceResult<OrganizationDto>.Fail(400, "NO_CHANGES", "אין שינויים לעדכון");
        if (org.Version != expectedVersion)
            return ServiceResult<OrganizationDto>.Fail(409, "VERSION_CONFLICT",
                "הרשומה עודכנה על ידי משתמש אחר. יש לטעון מחדש.");

        var oldStatus = org.Status;
        org.Status = "active";
        org.Version++;
        org.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.OrganizationRestore,
                OrganizationId = null,
                ActorUserId = actorUserId,
                EntityType = "organization",
                EntityId = org.Id,
                Action = "restore",
                FieldName = "status",
                OldValue = oldStatus,
                NewValue = "active",
                Reason = reason,
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationDto>.Fail(400, "VALIDATION_ERROR", ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<OrganizationDto>.Fail(500, "INTERNAL_ERROR", "שגיאת מערכת");
        }

        var hasOrgAdmin = await db.Users.AnyAsync(
            u => u.OrganizationId == org.Id && u.Role == Roles.OrganizationAdministrator,
            cancellationToken);
        return ServiceResult<OrganizationDto>.Ok(MapOrganization(org, hasOrgAdmin));
    }

    public async Task<ServiceResult<OrganizationDto>> EnterOrganizationAsync(
        Guid organizationId,
        Guid sessionId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var org = await db.Organizations.FindAsync([organizationId], cancellationToken);
        if (org is null)
            return ServiceResult<OrganizationDto>.Fail(404, "NOT_FOUND", "הארגון לא נמצא");
        if (org.Status != "active")
            return ServiceResult<OrganizationDto>.Fail(409, "ORG_SUSPENDED", "הארגון אינו פעיל");

        await sessionService.SetActingOrganizationAsync(sessionId, organizationId, cancellationToken);

        auditService.Stage(new AuditEntry
        {
            EventCode = BusinessEventCodes.SuperAdminEnterOrg,
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            EntityType = "organization",
            EntityId = organizationId,
            Action = "enter_org",
        });
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrganizationDto>.Ok(MapOrganization(org, hasOrgAdmin: true));
    }

    public async Task<ServiceResult<object>> ExitOrganizationAsync(
        Guid sessionId,
        Guid actorUserId,
        Guid? previousOrgId,
        CancellationToken cancellationToken = default)
    {
        await sessionService.SetActingOrganizationAsync(sessionId, null, cancellationToken);

        if (previousOrgId is not null)
        {
            auditService.Stage(new AuditEntry
            {
                EventCode = BusinessEventCodes.SuperAdminExitOrg,
                OrganizationId = previousOrgId,
                ActorUserId = actorUserId,
                EntityType = "organization",
                EntityId = previousOrgId.Value,
                Action = "exit_org",
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<object>.Ok(new { exited = true });
    }

    private static List<string> ValidateCreateRequest(CreateOrganizationRequest request)
    {
        var errors = new List<string>();
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length < 2 || name.Length > 200)
            errors.Add("שם הארגון הוא שדה חובה");

        var code = request.Code?.Trim() ?? string.Empty;
        if (code.Length < 2 || code.Length > 50)
            errors.Add("קוד הארגון הוא שדה חובה");
        else if (code != code.ToUpperInvariant() || !OrgCodeRegex.IsMatch(code))
            errors.Add("קוד הארגון חייב להיות באותיות גדולות, מספרים ומקף בלבד");

        return errors;
    }

    private static List<string> ValidateBootstrapRequest(BootstrapOrgAdminRequest request)
    {
        var errors = new List<string>();
        var username = request.Username?.Trim() ?? string.Empty;
        if (username.Length < 3 || username.Length > 100)
            errors.Add("שם משתמש הוא שדה חובה");

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8 || request.Password.Length > 128)
            errors.Add("סיסמה היא שדה חובה");

        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length < 2 || fullName.Length > 200)
            errors.Add("שם מלא הוא שדה חובה");

        return errors;
    }

    private static OrganizationDto MapOrganization(Organization org, bool hasOrgAdmin) => new()
    {
        Id = org.Id,
        Name = org.Name,
        Code = org.Code,
        Status = org.Status,
        Version = org.Version,
        CreatedAt = org.CreatedAt,
        UpdatedAt = org.UpdatedAt,
        HasOrgAdmin = hasOrgAdmin
    };
}

public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public int StatusCode { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public IReadOnlyList<string> Details { get; init; } = [];
    public object? StructuredDetails { get; init; }

    public static ServiceResult<T> Ok(T value) => new()
    {
        IsSuccess = true,
        Value = value,
        StatusCode = 200
    };

    public static ServiceResult<T> Fail(int statusCode, string code, string error, IReadOnlyList<string>? details = null) => new()
    {
        IsSuccess = false,
        StatusCode = statusCode,
        Code = code,
        Error = error,
        Details = details ?? []
    };

    public static ServiceResult<T> FailWithStructuredDetails(int statusCode, string code, string error, object structuredDetails) => new()
    {
        IsSuccess = false,
        StatusCode = statusCode,
        Code = code,
        Error = error,
        StructuredDetails = structuredDetails
    };
}
