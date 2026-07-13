using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

/// <summary>
/// Phase 16 M92 — ExportBatch / ExportBatchItem domain + amount-adjustment fields on AssistanceItem.
/// Soft-cancel history only; filtered unique index enforces one active export per PaymentExecution.
/// </summary>
public partial class ExportBatchAndAmountAdjustment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "original_approved_amount",
            table: "assistance_items",
            type: "numeric(14,2)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "previous_payment_amount",
            table: "assistance_items",
            type: "numeric(14,2)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "amount_adjustment_reason",
            table: "assistance_items",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "amount_adjustment_explanation",
            table: "assistance_items",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "amount_adjusted_by_user_id",
            table: "assistance_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "amount_adjusted_at",
            table: "assistance_items",
            type: "timestamp with time zone",
            nullable: true);

        // Backfill original approved amount for items already past approve (Amount is current).
        migrationBuilder.Sql("""
            UPDATE assistance_items
            SET original_approved_amount = amount
            WHERE status IN ('approved', 'waiting_for_reference', 'paid', 'completed', 'suspended')
              AND original_approved_amount IS NULL
              AND approved_at IS NOT NULL;
            """);

        migrationBuilder.CreateTable(
            name: "export_batches",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                batch_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                stored_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                total_item_count = table.Column<int>(type: "integer", nullable: false),
                active_item_count = table.Column<int>(type: "integer", nullable: false),
                cancelled_item_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_export_batches", x => x.id);
                table.ForeignKey(
                    name: "fk_export_batches_organizations_organization_id",
                    column: x => x.organization_id,
                    principalSchema: "public",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_export_batches_users_created_by_user_id",
                    column: x => x.created_by_user_id,
                    principalSchema: "public",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "export_batch_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                export_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                assistance_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                exported_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                decision_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                family_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                family_accounting_code = table.Column<long>(type: "bigint", nullable: true),
                family_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                assistance_type_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                assistance_type_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                original_approved_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                amount_adjustment_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                amount_adjustment_explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                supplier_accounting_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                payment_target = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                payee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                transfer_bank_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                transfer_branch_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                transfer_account_number = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                execution_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_export_batch_items", x => x.id);
                table.ForeignKey(
                    name: "fk_export_batch_items_assistance_items_assistance_item_id",
                    column: x => x.assistance_item_id,
                    principalSchema: "public",
                    principalTable: "assistance_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_export_batch_items_export_batches_export_batch_id",
                    column: x => x.export_batch_id,
                    principalSchema: "public",
                    principalTable: "export_batches",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_export_batch_items_organizations_organization_id",
                    column: x => x.organization_id,
                    principalSchema: "public",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_export_batch_items_payment_executions_payment_execution_id",
                    column: x => x.payment_execution_id,
                    principalSchema: "public",
                    principalTable: "payment_executions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_export_batch_items_users_cancelled_by_user_id",
                    column: x => x.cancelled_by_user_id,
                    principalSchema: "public",
                    principalTable: "users",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_assistance_items_amount_adjusted_by_user_id",
            table: "assistance_items",
            column: "amount_adjusted_by_user_id");

        migrationBuilder.AddForeignKey(
            name: "fk_assistance_items_users_amount_adjusted_by_user_id",
            table: "assistance_items",
            column: "amount_adjusted_by_user_id",
            principalSchema: "public",
            principalTable: "users",
            principalColumn: "id");

        migrationBuilder.CreateIndex(
            name: "ix_export_batches_org_created",
            table: "export_batches",
            columns: new[] { "organization_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_export_batches_org_status",
            table: "export_batches",
            columns: new[] { "organization_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ux_export_batches_org_batch_number",
            table: "export_batches",
            columns: new[] { "organization_id", "batch_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_export_batches_created_by_user_id",
            table: "export_batches",
            column: "created_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_export_batch_items_assistance_item_id",
            table: "export_batch_items",
            column: "assistance_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_export_batch_items_batch_status",
            table: "export_batch_items",
            columns: new[] { "export_batch_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_export_batch_items_cancelled_by_user_id",
            table: "export_batch_items",
            column: "cancelled_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_export_batch_items_org_item",
            table: "export_batch_items",
            columns: new[] { "organization_id", "assistance_item_id" });

        migrationBuilder.CreateIndex(
            name: "ix_export_batch_items_organization_id",
            table: "export_batch_items",
            column: "organization_id");

        // Critical anti-duplicate rule: at most one active ExportBatchItem per PaymentExecution.
        migrationBuilder.CreateIndex(
            name: "ux_export_batch_items_active_payment_execution",
            table: "export_batch_items",
            column: "payment_execution_id",
            unique: true,
            filter: "status = 'active'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "export_batch_items");

        migrationBuilder.DropTable(
            name: "export_batches");

        migrationBuilder.DropForeignKey(
            name: "fk_assistance_items_users_amount_adjusted_by_user_id",
            table: "assistance_items");

        migrationBuilder.DropIndex(
            name: "ix_assistance_items_amount_adjusted_by_user_id",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "amount_adjusted_at",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "amount_adjusted_by_user_id",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "amount_adjustment_explanation",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "amount_adjustment_reason",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "original_approved_amount",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "previous_payment_amount",
            table: "assistance_items");
    }
}
