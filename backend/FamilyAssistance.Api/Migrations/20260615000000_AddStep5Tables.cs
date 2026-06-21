using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class AddStep5Tables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "request_code_counter",
            table: "organizations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "assistance_requests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                request_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                family_id = table.Column<Guid>(type: "uuid", nullable: false),
                assistance_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                requested_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                is_urgent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                requested_by_coordinator_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assistance_requests", x => x.id);
                table.ForeignKey(
                    name: "FK_assistance_requests_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_assistance_requests_families_family_id",
                    column: x => x.family_id,
                    principalTable: "families",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "FK_assistance_requests_assistance_types_assistance_type_id",
                    column: x => x.assistance_type_id,
                    principalTable: "assistance_types",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "FK_assistance_requests_users_requested_by_coordinator_id",
                    column: x => x.requested_by_coordinator_id,
                    principalTable: "users",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "FK_assistance_requests_users_cancelled_by_user_id",
                    column: x => x.cancelled_by_user_id,
                    principalTable: "users",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ux_requests_org_code",
            table: "assistance_requests",
            columns: new[] { "organization_id", "request_code" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_requests_org_status",
            table: "assistance_requests",
            columns: new[] { "organization_id", "status" });
        migrationBuilder.CreateIndex(
            name: "ix_requests_org_coordinator_status",
            table: "assistance_requests",
            columns: new[] { "organization_id", "requested_by_coordinator_id", "status" });
        migrationBuilder.CreateIndex(
            name: "ix_requests_family",
            table: "assistance_requests",
            column: "family_id");
        migrationBuilder.CreateIndex(
            name: "ix_requests_type",
            table: "assistance_requests",
            column: "assistance_type_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "assistance_requests");
        migrationBuilder.DropColumn(name: "request_code_counter", table: "organizations");
    }
}
