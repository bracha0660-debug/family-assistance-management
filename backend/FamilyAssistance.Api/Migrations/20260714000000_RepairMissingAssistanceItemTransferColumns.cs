using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

/// <summary>
/// Phase 16.1 — Repair missing AssistanceItem transfer-bank columns when history
/// claims the original migration applied but physical columns are absent.
/// </summary>
public partial class RepairMissingAssistanceItemTransferColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public.assistance_items
                ADD COLUMN IF NOT EXISTS transfer_account_number character varying(34) NULL;

            ALTER TABLE public.assistance_items
                ADD COLUMN IF NOT EXISTS transfer_bank_number character varying(10) NULL;

            ALTER TABLE public.assistance_items
                ADD COLUMN IF NOT EXISTS transfer_branch_number character varying(10) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Forward-only repair: do not drop transfer_* columns.
        // Columns may have existed before this migration or may hold production data.
        // Application rollback retains the columns and this migration history row.
    }
}
