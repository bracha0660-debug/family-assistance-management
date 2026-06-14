using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class AddStep4Tables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "family_code_counter",
            table: "organizations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "families",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                family_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                head_of_household_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                head_id_number = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                household_size = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                assigned_coordinator_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_families", x => x.id);
                table.ForeignKey(
                    name: "FK_families_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_families_users_assigned_coordinator_id",
                    column: x => x.assigned_coordinator_id,
                    principalTable: "users",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "assistance_types",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                type_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                default_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assistance_types", x => x.id);
                table.ForeignKey(
                    name: "FK_assistance_types_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_families_org_code",
            table: "families",
            columns: new[] { "organization_id", "family_code" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_families_org_status",
            table: "families",
            columns: new[] { "organization_id", "status" });
        migrationBuilder.CreateIndex(
            name: "ix_families_org_coordinator_status",
            table: "families",
            columns: new[] { "organization_id", "assigned_coordinator_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ux_assistance_types_org_code",
            table: "assistance_types",
            columns: new[] { "organization_id", "type_code" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_assistance_types_org_status",
            table: "assistance_types",
            columns: new[] { "organization_id", "status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "assistance_types");
        migrationBuilder.DropTable(name: "families");
        migrationBuilder.DropColumn(name: "family_code_counter", table: "organizations");
    }
}
