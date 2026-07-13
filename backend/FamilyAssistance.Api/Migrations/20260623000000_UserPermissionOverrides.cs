using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class UserPermissionOverrides : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_permission_overrides",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                permission_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                effect = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_permission_overrides", x => x.id);
                table.CheckConstraint(
                    "ck_user_perm_overrides_effect",
                    "effect IN ('grant', 'deny')");
                table.CheckConstraint(
                    "ck_user_perm_overrides_scope",
                    "effect = 'deny' OR scope IS NOT NULL");
                table.ForeignKey(
                    name: "FK_user_permission_overrides_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_user_permission_overrides_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_permission_overrides_permission_catalog_permission_key",
                    column: x => x.permission_key,
                    principalTable: "permission_catalog",
                    principalColumn: "permission_key",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_user_permission_overrides_users_granted_by_user_id",
                    column: x => x.granted_by_user_id,
                    principalTable: "users",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "ux_user_perm_overrides_user_key",
            table: "user_permission_overrides",
            columns: new[] { "user_id", "permission_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_user_perm_overrides_org_id",
            table: "user_permission_overrides",
            column: "organization_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_permission_overrides");
    }
}
