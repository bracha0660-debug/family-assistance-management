using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class FamilyCardSpecAndStep15 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bank_account_history");
        migrationBuilder.DropTable(name: "bank_accounts");

        migrationBuilder.DropIndex(name: "ux_families_org_accounting_number", table: "families");

        migrationBuilder.DropColumn(name: "number_of_children", table: "families");
        migrationBuilder.DropColumn(name: "notes", table: "families");
        migrationBuilder.DropColumn(name: "accounting_number_counter", table: "organizations");

        migrationBuilder.RenameColumn(
            name: "external_accounting_number",
            table: "families",
            newName: "accounting_code");

        migrationBuilder.AddColumn<Guid>(
            name: "accounting_coordinator_id",
            table: "families",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "bank_number",
            table: "families",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "branch_number",
            table: "families",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "account_number",
            table: "families",
            type: "character varying(34)",
            maxLength: 34,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "account_holder_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "bank_verified_externally",
            table: "families",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""
            UPDATE families
            SET accounting_coordinator_id = assigned_coordinator_id,
                bank_number = '12',
                branch_number = '345',
                account_number = '1234567',
                account_holder_name = COALESCE(NULLIF(TRIM(family_last_name), ''), 'לא ידוע')
            WHERE accounting_coordinator_id IS NULL;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "accounting_coordinator_id",
            table: "families",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "bank_number",
            table: "families",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "branch_number",
            table: "families",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "account_number",
            table: "families",
            type: "character varying(34)",
            maxLength: 34,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(34)",
            oldMaxLength: 34,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "account_holder_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_families_org_acct_coord_code",
            table: "families",
            columns: new[] { "organization_id", "accounting_coordinator_id", "accounting_code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_families_org_father_id",
            table: "families",
            columns: new[] { "organization_id", "father_israeli_id" },
            filter: "father_israeli_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_families_org_mother_id",
            table: "families",
            columns: new[] { "organization_id", "mother_israeli_id" },
            filter: "mother_israeli_id IS NOT NULL");

        migrationBuilder.AddColumn<int>(
            name: "supplier_code_counter",
            table: "organizations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "decision_code_counter",
            table: "organizations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "suppliers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                supplier_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                registration_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                bank_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                branch_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                account_number = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                account_holder_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                bank_verified_externally = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_suppliers", x => x.id);
                table.ForeignKey(
                    name: "FK_suppliers_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_suppliers_org_code",
            table: "suppliers",
            columns: new[] { "organization_id", "supplier_code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_suppliers_org_status",
            table: "suppliers",
            columns: new[] { "organization_id", "status" });

        migrationBuilder.CreateTable(
            name: "committee_decisions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                decision_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                family_id = table.Column<Guid>(type: "uuid", nullable: false),
                meeting_date = table.Column<DateOnly>(type: "date", nullable: false),
                is_urgent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                total_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0m),
                rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                suspend_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                return_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                suspended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_committee_decisions", x => x.id);
                table.ForeignKey(
                    name: "FK_committee_decisions_families_family_id",
                    column: x => x.family_id,
                    principalTable: "families",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_committee_decisions_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_committee_decisions_users_created_by_user_id",
                    column: x => x.created_by_user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_committee_decisions_org_code",
            table: "committee_decisions",
            columns: new[] { "organization_id", "decision_code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_committee_decisions_org_status",
            table: "committee_decisions",
            columns: new[] { "organization_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_committee_decisions_family",
            table: "committee_decisions",
            column: "family_id");

        migrationBuilder.CreateTable(
            name: "assistance_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                committee_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                line_number = table.Column<int>(type: "integer", nullable: false),
                assistance_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                payment_target = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                payee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                voucher_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                execution_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                execution_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assistance_items", x => x.id);
                table.ForeignKey(
                    name: "FK_assistance_items_assistance_types_assistance_type_id",
                    column: x => x.assistance_type_id,
                    principalTable: "assistance_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_assistance_items_committee_decisions_committee_decision_id",
                    column: x => x.committee_decision_id,
                    principalTable: "committee_decisions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_assistance_items_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_assistance_items_suppliers_supplier_id",
                    column: x => x.supplier_id,
                    principalTable: "suppliers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_assistance_items_decision_line",
            table: "assistance_items",
            columns: new[] { "committee_decision_id", "line_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_assistance_items_org_execution",
            table: "assistance_items",
            columns: new[] { "organization_id", "execution_status" });

        migrationBuilder.CreateTable(
            name: "assistance_item_documents",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                assistance_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                stored_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assistance_item_documents", x => x.id);
                table.ForeignKey(
                    name: "FK_assistance_item_documents_assistance_items_assistance_item_id",
                    column: x => x.assistance_item_id,
                    principalTable: "assistance_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_assistance_item_documents_users_uploaded_by_user_id",
                    column: x => x.uploaded_by_user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_assistance_item_documents_item",
            table: "assistance_item_documents",
            column: "assistance_item_id",
            unique: true);

        migrationBuilder.CreateTable(
            name: "payment_executions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                committee_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                assistance_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                execution_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                proof_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                proof_stored_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                return_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                proof_uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                returned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_executions", x => x.id);
                table.ForeignKey(
                    name: "FK_payment_executions_assistance_items_assistance_item_id",
                    column: x => x.assistance_item_id,
                    principalTable: "assistance_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_payment_executions_committee_decisions_committee_decision_id",
                    column: x => x.committee_decision_id,
                    principalTable: "committee_decisions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_payment_executions_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_payment_executions_item",
            table: "payment_executions",
            column: "assistance_item_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_payment_executions_org_status",
            table: "payment_executions",
            columns: new[] { "organization_id", "status" });

        migrationBuilder.Sql("""
            INSERT INTO permission_catalog (permission_key, category, display_name_he, description_he, sort_order, is_active, supports_my_records, scope_applies)
            VALUES
              ('payments.view', 'payments', 'צפייה בתשלומים', 'צפייה בתור תשלומים, בסטטוס ביצוע ובפרטי תשלום', 10, true, false, true),
              ('payments.execute', 'payments', 'ביצוע תשלום', 'ייזום/רישום ביצוע תשלום לפריט מאושר', 20, true, false, true),
              ('payments.upload_proof', 'payments', 'העלאת אישור ביצוע', 'העלאת מסמך אישור ביצוע (קבלה, אישור בנק וכו'')', 30, true, false, true),
              ('payments.mark_paid', 'payments', 'סימון כשולם', 'סימון תשלום כשולם — מעבר לסטטוס סופי', 40, true, false, true),
              ('payments.return_to_coordinator', 'payments', 'החזרה לרכז', 'החזרת פריט תשלום לרכז לתיקון/השלמה', 50, true, false, true)
            ON CONFLICT (permission_key) DO NOTHING;
            """);

        migrationBuilder.Sql("""
            INSERT INTO organization_role_grants (organization_role_id, permission_key, scope, granted_at)
            SELECT r.id, g.permission_key, g.scope, NOW()
            FROM organization_roles r
            CROSS JOIN (VALUES
              ('payments.view', 'organization'),
              ('payments.execute', 'organization'),
              ('payments.upload_proof', 'organization'),
              ('payments.mark_paid', 'organization'),
              ('payments.return_to_coordinator', 'organization')
            ) AS g(permission_key, scope)
            WHERE r.factory_preset_key = 'preset_finance'
              AND NOT EXISTS (
                SELECT 1 FROM organization_role_grants existing
                WHERE existing.organization_role_id = r.id
                  AND existing.permission_key = g.permission_key
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM organization_role_grants
            WHERE permission_key LIKE 'payments.%';
            DELETE FROM permission_catalog WHERE permission_key LIKE 'payments.%';
            """);

        migrationBuilder.DropTable(name: "payment_executions");
        migrationBuilder.DropTable(name: "assistance_item_documents");
        migrationBuilder.DropTable(name: "assistance_items");
        migrationBuilder.DropTable(name: "committee_decisions");
        migrationBuilder.DropTable(name: "suppliers");

        migrationBuilder.DropIndex(name: "ux_families_org_acct_coord_code", table: "families");
        migrationBuilder.DropIndex(name: "ix_families_org_father_id", table: "families");
        migrationBuilder.DropIndex(name: "ix_families_org_mother_id", table: "families");

        migrationBuilder.DropColumn(name: "accounting_coordinator_id", table: "families");
        migrationBuilder.DropColumn(name: "bank_number", table: "families");
        migrationBuilder.DropColumn(name: "branch_number", table: "families");
        migrationBuilder.DropColumn(name: "account_number", table: "families");
        migrationBuilder.DropColumn(name: "account_holder_name", table: "families");
        migrationBuilder.DropColumn(name: "bank_verified_externally", table: "families");
        migrationBuilder.DropColumn(name: "supplier_code_counter", table: "organizations");
        migrationBuilder.DropColumn(name: "decision_code_counter", table: "organizations");

        migrationBuilder.RenameColumn(
            name: "accounting_code",
            table: "families",
            newName: "external_accounting_number");

        migrationBuilder.AddColumn<int>(
            name: "number_of_children",
            table: "families",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "notes",
            table: "families",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "accounting_number_counter",
            table: "organizations",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateIndex(
            name: "ux_families_org_accounting_number",
            table: "families",
            columns: new[] { "organization_id", "external_accounting_number" },
            unique: true);
    }
}
