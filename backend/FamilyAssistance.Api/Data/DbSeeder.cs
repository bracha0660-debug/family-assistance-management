using FamilyAssistance.Api.Constants;
using FamilyAssistance.Api.Entities;
using FamilyAssistance.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

    private static readonly RequiredColumnContract[] RequiredColumnContracts =
    [
        new("public", "assistance_items", "transfer_account_number", "character varying", 34, "YES"),
        new("public", "assistance_items", "transfer_bank_number", "character varying", 10, "YES"),
        new("public", "assistance_items", "transfer_branch_number", "character varying", 10, "YES"),
    ];

    public static async Task SeedAsync(
        AppDbContext db,
        PermissionService permissionService,
        IConfiguration configuration,
        ILogger logger,
        IHostEnvironment hostEnvironment)
    {
        await EnsureDatabaseSchemaAsync(db, logger, hostEnvironment);
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

    internal static bool AllowEnsureCreatedFallback(string environmentName)
        => !string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase);

    internal static string FormatColumnContractMismatch(
        RequiredColumnContract expected,
        ColumnDefinition? detected)
    {
        var expectedText =
            $"{expected.DataType}({expected.MaxLength}) nullable={expected.IsNullable}";

        if (detected is null)
        {
            return
                $"Database schema invalid. Missing column {expected.Schema}.{expected.Table}.{expected.Column}. " +
                $"Expected: {expectedText}. Detected: missing.";
        }

        var detectedText =
            $"{detected.DataType}({detected.MaxLength?.ToString() ?? "null"}) nullable={detected.IsNullable}";

        return
            $"Database schema invalid. Column contract mismatch for {expected.Schema}.{expected.Table}.{expected.Column}. " +
            $"Expected: {expectedText}. Detected: {detectedText}.";
    }

    internal static bool MatchesColumnContract(RequiredColumnContract expected, ColumnDefinition detected)
        => string.Equals(detected.DataType, expected.DataType, StringComparison.OrdinalIgnoreCase)
           && detected.MaxLength == expected.MaxLength
           && string.Equals(detected.IsNullable, expected.IsNullable, StringComparison.OrdinalIgnoreCase);

    private static async Task EnsureDatabaseSchemaAsync(
        AppDbContext db,
        ILogger logger,
        IHostEnvironment hostEnvironment)
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
            var missingTables = await GetMissingTablesAsync(db);
            if (!AllowEnsureCreatedFallback(hostEnvironment.EnvironmentName))
            {
                throw new InvalidOperationException(
                    $"Database schema incomplete in Production. Missing tables: {string.Join(", ", missingTables)}. " +
                    "EnsureCreatedAsync is not permitted in Production.");
            }

            logger.LogWarning(
                "Required tables missing after migration in {Environment}; creating schema via EnsureCreated (non-Production only)",
                hostEnvironment.EnvironmentName);
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

        logger.LogInformation(
            "Database schema verified: all {Count} required tables exist",
            RequiredTables.Length);

        await VerifyRequiredColumnContractsAsync(db, logger);

        logger.LogInformation(
            "Database schema verified: required tables and required column contracts are valid.");
    }

    private static async Task VerifyRequiredColumnContractsAsync(AppDbContext db, ILogger logger)
    {
        foreach (var contract in RequiredColumnContracts)
        {
            var detected = await GetColumnDefinitionAsync(db, contract.Schema, contract.Table, contract.Column);
            if (detected is null || !MatchesColumnContract(contract, detected))
            {
                var message = FormatColumnContractMismatch(contract, detected);
                logger.LogError("{SchemaValidationError}", message);
                throw new InvalidOperationException(message);
            }
        }

        logger.LogInformation(
            "Database schema verified: {Count} required column contracts match",
            RequiredColumnContracts.Length);
    }

    private static async Task<ColumnDefinition?> GetColumnDefinitionAsync(
        AppDbContext db,
        string schema,
        string table,
        string column)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT data_type, character_maximum_length, is_nullable
                FROM information_schema.columns
                WHERE table_schema = @schema
                  AND table_name = @table
                  AND column_name = @column
                """;

            AddParameter(command, "@schema", schema);
            AddParameter(command, "@table", table);
            AddParameter(command, "@column", column);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var dataType = reader.GetString(0);
            int? maxLength = reader.IsDBNull(1) ? null : Convert.ToInt32(reader.GetValue(1));
            var isNullable = reader.GetString(2);
            return new ColumnDefinition(dataType, maxLength, isNullable);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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

    internal sealed record RequiredColumnContract(
        string Schema,
        string Table,
        string Column,
        string DataType,
        int MaxLength,
        string IsNullable);

    internal sealed record ColumnDefinition(
        string DataType,
        int? MaxLength,
        string IsNullable);
}
