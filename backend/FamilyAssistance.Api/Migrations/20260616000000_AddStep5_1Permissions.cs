using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class AddStep5_1Permissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "permission_catalog",
            columns: table => new
            {
                permission_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                display_name_he = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description_he = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_permission_catalog", x => x.permission_key);
            });

        migrationBuilder.CreateIndex(
            name: "ix_permission_catalog_category",
            table: "permission_catalog",
            columns: new[] { "category", "sort_order" });

        migrationBuilder.CreateTable(
            name: "organization_role_permissions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                permission_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organization_role_permissions", x => x.id);
                table.ForeignKey(
                    name: "FK_organization_role_permissions_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_organization_role_permissions_permission_catalog_permission_key",
                    column: x => x.permission_key,
                    principalTable: "permission_catalog",
                    principalColumn: "permission_key",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_organization_role_permissions_users_granted_by_user_id",
                    column: x => x.granted_by_user_id,
                    principalTable: "users",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_org_role_permissions_org_role",
            table: "organization_role_permissions",
            columns: new[] { "organization_id", "role" });

        migrationBuilder.CreateIndex(
            name: "ix_org_role_permissions_org_key",
            table: "organization_role_permissions",
            columns: new[] { "organization_id", "permission_key" });

        migrationBuilder.CreateIndex(
            name: "ux_org_role_permissions_org_role_key",
            table: "organization_role_permissions",
            columns: new[] { "organization_id", "role", "permission_key" },
            unique: true);

        migrationBuilder.Sql("""
            INSERT INTO permission_catalog (permission_key, category, display_name_he, description_he, sort_order, is_active) VALUES
            ('families.view', 'families', 'צפייה במשפחות', NULL, 10, true),
            ('families.create', 'families', 'יצירת משפחות', NULL, 20, true),
            ('families.edit', 'families', 'עריכת משפחות', NULL, 30, true),
            ('families.deactivate', 'families', 'השבתת משפחות', NULL, 40, true),
            ('assistance_types.view', 'assistance_types', 'צפייה בסוגי סיוע', NULL, 10, true),
            ('assistance_types.create', 'assistance_types', 'יצירת סוגי סיוע', NULL, 20, true),
            ('assistance_types.edit', 'assistance_types', 'עריכת סוגי סיוע', NULL, 30, true),
            ('assistance_types.deactivate', 'assistance_types', 'השבתת סוגי סיוע', NULL, 40, true),
            ('assistance_requests.view', 'assistance_requests', 'צפייה בבקשות סיוע', NULL, 10, true),
            ('assistance_requests.create', 'assistance_requests', 'יצירת בקשות סיוע', NULL, 20, true),
            ('assistance_requests.edit', 'assistance_requests', 'עריכת בקשות סיוע', NULL, 30, true),
            ('assistance_requests.cancel', 'assistance_requests', 'ביטול בקשות סיוע', NULL, 40, true),
            ('users.view', 'users', 'צפייה במשתמשים', NULL, 10, true),
            ('users.create', 'users', 'יצירת משתמשים', NULL, 20, true),
            ('users.edit', 'users', 'עריכת משתמשים', NULL, 30, true),
            ('users.disable', 'users', 'השבתת משתמשים', NULL, 40, true),
            ('activity_log.view', 'activity_log', 'צפייה ביומן פעילות', NULL, 10, true)
            ON CONFLICT (permission_key) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "organization_role_permissions");
        migrationBuilder.DropTable(name: "permission_catalog");
    }
}
