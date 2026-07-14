using System.Reflection;
using FamilyAssistance.Api.Data;
using FamilyAssistance.Api.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Hosting;

namespace FamilyAssistance.Api.Tests;

public class SchemaRepairPhase161Tests
{
    [Fact]
    public void AllowEnsureCreatedFallback_IsFalse_InProduction()
    {
        Assert.False(DbSeeder.AllowEnsureCreatedFallback(Environments.Production));
        Assert.False(DbSeeder.AllowEnsureCreatedFallback("Production"));
        Assert.False(DbSeeder.AllowEnsureCreatedFallback("PRODUCTION"));
    }

    [Fact]
    public void AllowEnsureCreatedFallback_IsTrue_OutsideProduction()
    {
        Assert.True(DbSeeder.AllowEnsureCreatedFallback(Environments.Development));
        Assert.True(DbSeeder.AllowEnsureCreatedFallback(Environments.Staging));
        Assert.True(DbSeeder.AllowEnsureCreatedFallback("Test"));
    }

    [Fact]
    public void MatchesColumnContract_AcceptsExactTransferContracts()
    {
        var expected = new DbSeeder.RequiredColumnContract(
            "public", "assistance_items", "transfer_account_number", "character varying", 34, "YES");
        var detected = new DbSeeder.ColumnDefinition("character varying", 34, "YES");

        Assert.True(DbSeeder.MatchesColumnContract(expected, detected));
    }

    [Fact]
    public void MatchesColumnContract_RejectsLengthTypeOrNullabilityMismatch()
    {
        var expected = new DbSeeder.RequiredColumnContract(
            "public", "assistance_items", "transfer_bank_number", "character varying", 10, "YES");

        Assert.False(DbSeeder.MatchesColumnContract(expected, new DbSeeder.ColumnDefinition("character varying", 20, "YES")));
        Assert.False(DbSeeder.MatchesColumnContract(expected, new DbSeeder.ColumnDefinition("text", 10, "YES")));
        Assert.False(DbSeeder.MatchesColumnContract(expected, new DbSeeder.ColumnDefinition("character varying", 10, "NO")));
    }

    [Fact]
    public void FormatColumnContractMismatch_IdentifiesMissingAndDetectedWithoutSecrets()
    {
        var expected = new DbSeeder.RequiredColumnContract(
            "public", "assistance_items", "transfer_branch_number", "character varying", 10, "YES");

        var missing = DbSeeder.FormatColumnContractMismatch(expected, null);
        Assert.Contains("public.assistance_items.transfer_branch_number", missing);
        Assert.Contains("Detected: missing", missing);
        Assert.DoesNotContain("Password", missing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Connection", missing, StringComparison.OrdinalIgnoreCase);

        var mismatch = DbSeeder.FormatColumnContractMismatch(
            expected,
            new DbSeeder.ColumnDefinition("character varying", 5, "NO"));
        Assert.Contains("Expected: character varying(10) nullable=YES", mismatch);
        Assert.Contains("Detected: character varying(5) nullable=NO", mismatch);
    }

    [Fact]
    public void RepairMigration_HasExactId_AndGuardedSql_AndForwardOnlyDown()
    {
        var migrationAttribute = typeof(RepairMissingAssistanceItemTransferColumns)
            .GetCustomAttributes(typeof(MigrationAttribute), inherit: false)
            .Cast<MigrationAttribute>()
            .Single();

        Assert.Equal("20260714000000_RepairMissingAssistanceItemTransferColumns", migrationAttribute.Id);

        var migration = new RepairMissingAssistanceItemTransferColumns();
        var upMethod = typeof(RepairMissingAssistanceItemTransferColumns)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var downMethod = typeof(RepairMissingAssistanceItemTransferColumns)
            .GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(upMethod);
        Assert.NotNull(downMethod);

        var upBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        upMethod!.Invoke(migration, [upBuilder]);

        var sqlOp = Assert.Single(upBuilder.Operations.OfType<SqlOperation>());
        Assert.Contains("ADD COLUMN IF NOT EXISTS transfer_account_number character varying(34) NULL", sqlOp.Sql);
        Assert.Contains("ADD COLUMN IF NOT EXISTS transfer_bank_number character varying(10) NULL", sqlOp.Sql);
        Assert.Contains("ADD COLUMN IF NOT EXISTS transfer_branch_number character varying(10) NULL", sqlOp.Sql);

        var downBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        downMethod!.Invoke(migration, [downBuilder]);
        Assert.Empty(downBuilder.Operations);
    }
}
