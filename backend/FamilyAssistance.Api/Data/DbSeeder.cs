using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FamilyAssistance.Api.Data;

public static class DbSeeder
{
    private static readonly string[] RequiredTables =
    [
        "organizations",
        "users",
        "user_sessions",
        "audit_logs",
        "security_audit_logs",
        "families",
        "suppliers",
        "assistance_types",
        "committee_decisions",
        "assistance_items",
        "assistance_item_documents",
        "payment_executions",
        "permission_catalog",
        "organization_roles",
        "organization_role_grants",
    ];

    public static async Task SeedAsync(
        AppDbContext db,
        PermissionService permissionService,
        IConfiguration configuration,
        ILogger logger)
    {
        await EnsureDatabaseSchemaAsync(db, logger);
        await permissionService.SeedCatalogAsync();
        await permissionService.EnsureAllOrganizationsHaveRolesAsync();

        if (await db.Users.AnyAsync(u => u.Role == Roles.SuperAdmin))
            return;

        var password = configuration["SUPERADMIN_INITIAL_PASSWORD"]
            ?? Environment.GetEnvironmentVariable("SUPERADMIN_INITIAL_PASSWORD")
            ?? "ChangeMe123!";

        var hasher = new PasswordHasher<User>();
        var now = DateTime.UtcNow;
        var superAdmin = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = null,
            Username = "superadmin",
            FullName = "Super Administrator",
            Role = Roles.SuperAdmin,
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        superAdmin.PasswordHash = hasher.HashPassword(superAdmin, password);

        db.Users.Add(superAdmin);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded SuperAdmin user 'superadmin'.");
    }

    private static async Task EnsureDatabaseSchemaAsync(AppDbContext db, ILogger logger)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        logger.LogInformation(
            "Database migrations: {AppliedCount} applied, {PendingCount} pending",
            applied.Count,
            pending.Count);

        if (pending.Count > 0)
        {
            logger.LogInformation("Applying migrations: {Migrations}", string.Join(", ", pending));
            await db.Database.MigrateAsync();
        }

        if (!await AllRequiredTablesExistAsync(db))
        {
            logger.LogWarning("Required tables missing after migration; creating schema via EnsureCreated");
            var created = await db.Database.EnsureCreatedAsync();
            logger.LogInformation("EnsureCreated returned {Created}", created);
            await EnsurePartialIndexesAsync(db);
        }

        var missing = await GetMissingTablesAsync(db);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Database schema incomplete. Missing tables: {string.Join(", ", missing)}");
        }

        logger.LogInformation("Database schema verified: all {Count} required tables exist", RequiredTables.Length);
    }

    private static async Task<bool> AllRequiredTablesExistAsync(AppDbContext db)
    {
        var missing = await GetMissingTablesAsync(db);
        return missing.Count == 0;
    }

    private static async Task<List<string>> GetMissingTablesAsync(AppDbContext db)
    {
        var missing = new List<string>();
        foreach (var table in RequiredTables)
        {
            if (!await TableExistsAsync(db, table))
                missing.Add(table);
        }
        return missing;
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string tableName)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = @tableName
                )
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            return result is bool exists && exists;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsurePartialIndexesAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS ux_families_org_acct_coord_code
                ON families (organization_id, accounting_coordinator_id, accounting_code);
            """);
    }
}
