using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class PermissionsSystemScopeAndRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "supports_my_records",
            table: "permission_catalog",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "scope_applies",
            table: "permission_catalog",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<Guid>(
            name: "organization_role_id",
            table: "users",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "acting_organization_id",
            table: "user_sessions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "organization_roles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                factory_preset_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organization_roles", x => x.id);
                table.ForeignKey(
                    name: "FK_organization_roles_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "organization_role_grants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                permission_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organization_role_grants", x => x.id);
                table.ForeignKey(
                    name: "FK_organization_role_grants_organization_roles_organization_role_id",
                    column: x => x.organization_role_id,
                    principalTable: "organization_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_organization_role_grants_permission_catalog_permission_key",
                    column: x => x.permission_key,
                    principalTable: "permission_catalog",
                    principalColumn: "permission_key",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_organization_role_grants_users_granted_by_user_id",
                    column: x => x.granted_by_user_id,
                    principalTable: "users",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(name: "ix_users_organization_role_id", table: "users", column: "organization_role_id");
        migrationBuilder.CreateIndex(name: "ix_user_sessions_acting_org", table: "user_sessions", column: "acting_organization_id");
        migrationBuilder.CreateIndex(name: "ux_organization_roles_org_name", table: "organization_roles", columns: new[] { "organization_id", "name" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_organization_roles_org_id", table: "organization_roles", column: "organization_id");
        migrationBuilder.CreateIndex(name: "ix_organization_roles_org_status", table: "organization_roles", columns: new[] { "organization_id", "status" });
        migrationBuilder.CreateIndex(name: "ix_org_role_grants_role_id", table: "organization_role_grants", column: "organization_role_id");
        migrationBuilder.CreateIndex(name: "ix_org_role_grants_permission_key", table: "organization_role_grants", column: "permission_key");
        migrationBuilder.CreateIndex(name: "ux_org_role_grants_role_key", table: "organization_role_grants", columns: new[] { "organization_role_id", "permission_key" }, unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_users_organization_roles_organization_role_id",
            table: "users",
            column: "organization_role_id",
            principalTable: "organization_roles",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_user_sessions_organizations_acting_organization_id",
            table: "user_sessions",
            column: "acting_organization_id",
            principalTable: "organizations",
            principalColumn: "id");

        migrationBuilder.Sql("""
            DELETE FROM organization_role_permissions
            WHERE permission_key IN (
                'users.view','users.create','users.edit','users.disable',
                'activity_log.view',
                'assistance_requests.view','assistance_requests.create',
                'assistance_requests.edit','assistance_requests.cancel'
            );

            DELETE FROM permission_catalog
            WHERE permission_key IN (
                'users.view','users.create','users.edit','users.disable',
                'activity_log.view',
                'assistance_requests.view','assistance_requests.create',
                'assistance_requests.edit','assistance_requests.cancel'
            );
            """);

        migrationBuilder.Sql(PermissionCatalogUpsertSql);

        migrationBuilder.Sql("""
            DO $$
            DECLARE
                org RECORD;
                role_coord UUID;
                role_mgr UUID;
                role_fin UUID;
                now_ts TIMESTAMPTZ := now();
            BEGIN
                FOR org IN SELECT id FROM organizations LOOP
                    INSERT INTO organization_roles (id, organization_id, factory_preset_key, name, description, status, version, created_at, updated_at)
                    SELECT gen_random_uuid(), org.id, 'preset_coordinator', 'רכז/ת', 'תפקיד ברירת מחדל — נקודת התחלה לעבודה שטח', 'active', 1, now_ts, now_ts
                    WHERE NOT EXISTS (SELECT 1 FROM organization_roles r WHERE r.organization_id = org.id AND r.factory_preset_key = 'preset_coordinator');

                    INSERT INTO organization_roles (id, organization_id, factory_preset_key, name, description, status, version, created_at, updated_at)
                    SELECT gen_random_uuid(), org.id, 'preset_manager', 'מנהל/ת', 'תפקיד ברירת מחדל — נקודת התחלה לצפייה ואישור', 'active', 1, now_ts, now_ts
                    WHERE NOT EXISTS (SELECT 1 FROM organization_roles r WHERE r.organization_id = org.id AND r.factory_preset_key = 'preset_manager');

                    INSERT INTO organization_roles (id, organization_id, factory_preset_key, name, description, status, version, created_at, updated_at)
                    SELECT gen_random_uuid(), org.id, 'preset_finance', 'כספים', 'תפקיד ברירת מחדל — נקודת התחלה לניהול סוגי סיוע', 'active', 1, now_ts, now_ts
                    WHERE NOT EXISTS (SELECT 1 FROM organization_roles r WHERE r.organization_id = org.id AND r.factory_preset_key = 'preset_finance');

                    SELECT id INTO role_coord FROM organization_roles WHERE organization_id = org.id AND factory_preset_key = 'preset_coordinator' LIMIT 1;
                    SELECT id INTO role_mgr FROM organization_roles WHERE organization_id = org.id AND factory_preset_key = 'preset_manager' LIMIT 1;
                    SELECT id INTO role_fin FROM organization_roles WHERE organization_id = org.id AND factory_preset_key = 'preset_finance' LIMIT 1;

                    -- coordinator seed (12)
                    INSERT INTO organization_role_grants (id, organization_role_id, permission_key, scope, granted_at)
                    SELECT gen_random_uuid(), role_coord, t.k, t.s, now_ts FROM (VALUES
                        ('families.view','my_records'),('families.create','organization'),('families.edit','my_records'),('families.deactivate','my_records'),
                        ('committee_decisions.view','my_records'),('committee_decisions.create','organization'),('committee_decisions.edit_draft','my_records'),
                        ('committee_decisions.submit','my_records'),('assistance_items.view','my_records'),('assistance_items.create','organization'),
                        ('assistance_items.edit','my_records'),('assistance_items.remove_draft','my_records')
                    ) AS t(k,s)
                    WHERE NOT EXISTS (SELECT 1 FROM organization_role_grants g WHERE g.organization_role_id = role_coord AND g.permission_key = t.k);

                    INSERT INTO organization_role_grants (id, organization_role_id, permission_key, scope, granted_at)
                    SELECT gen_random_uuid(), role_mgr, t.k, 'organization', now_ts FROM (VALUES
                        ('families.view'),('assistance_types.view'),('committee_decisions.view'),('committee_decisions.approve'),
                        ('committee_decisions.reject'),('committee_decisions.cancel'),('assistance_items.view'),('suppliers.view')
                    ) AS t(k)
                    WHERE NOT EXISTS (SELECT 1 FROM organization_role_grants g WHERE g.organization_role_id = role_mgr AND g.permission_key = t.k);

                    INSERT INTO organization_role_grants (id, organization_role_id, permission_key, scope, granted_at)
                    SELECT gen_random_uuid(), role_fin, t.k, 'organization', now_ts FROM (VALUES
                        ('assistance_types.view'),('assistance_types.create'),('assistance_types.edit'),('assistance_types.deactivate'),('assistance_types.restore'),
                        ('suppliers.view'),('suppliers.edit'),('families.view'),('committee_decisions.view'),('assistance_items.view')
                    ) AS t(k)
                    WHERE NOT EXISTS (SELECT 1 FROM organization_role_grants g WHERE g.organization_role_id = role_fin AND g.permission_key = t.k);
                END LOOP;
            END $$;
            """);

        migrationBuilder.Sql("""
            UPDATE users u SET organization_role_id = r.id
            FROM organization_roles r
            WHERE u.organization_id = r.organization_id
              AND u.role = 'Coordinator' AND r.factory_preset_key = 'preset_coordinator';

            UPDATE users u SET organization_role_id = r.id
            FROM organization_roles r
            WHERE u.organization_id = r.organization_id
              AND u.role = 'Manager' AND r.factory_preset_key = 'preset_manager';

            UPDATE users u SET organization_role_id = r.id
            FROM organization_roles r
            WHERE u.organization_id = r.organization_id
              AND u.role = 'Finance' AND r.factory_preset_key = 'preset_finance';

            UPDATE users SET role = 'OrganizationUser'
            WHERE role IN ('Coordinator', 'Manager', 'Finance');
            """);

        migrationBuilder.DropTable(name: "organization_role_permissions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
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
            constraints: table => { table.PrimaryKey("PK_organization_role_permissions", x => x.id); });

        migrationBuilder.DropForeignKey(name: "FK_users_organization_roles_organization_role_id", table: "users");
        migrationBuilder.DropForeignKey(name: "FK_user_sessions_organizations_acting_organization_id", table: "user_sessions");
        migrationBuilder.DropTable(name: "organization_role_grants");
        migrationBuilder.DropTable(name: "organization_roles");
        migrationBuilder.DropColumn(name: "organization_role_id", table: "users");
        migrationBuilder.DropColumn(name: "acting_organization_id", table: "user_sessions");
        migrationBuilder.DropColumn(name: "supports_my_records", table: "permission_catalog");
        migrationBuilder.DropColumn(name: "scope_applies", table: "permission_catalog");
    }

    private const string PermissionCatalogUpsertSql = """
        INSERT INTO permission_catalog (permission_key, category, display_name_he, description_he, sort_order, is_active, supports_my_records, scope_applies) VALUES
        ('families.view','families','צפייה במשפחות','צפייה ברשימת משפחות ובפרטי משפחה',10,true,true,true),
        ('families.create','families','יצירת משפחות','הוספת משפחה חדשה לארגון',20,true,false,false),
        ('families.edit','families','עריכת משפחות','עדכון פרטי משפחה',30,true,true,true),
        ('families.deactivate','families','השבתת משפחות','השבתת משפחה (לא מחיקה)',40,true,true,true),
        ('families.restore','families','שחזור משפחות','שחזור משפחה שהושבתה',50,true,true,true),
        ('families.export','families','ייצוא משפחות','ייצוא נתוני משפחות',60,true,true,true),
        ('suppliers.view','suppliers','צפייה בספקים','צפייה ברשימת ספקים ובפרטי ספק',10,true,false,true),
        ('suppliers.create','suppliers','יצירת ספקים','הוספת ספק חדש',20,true,false,false),
        ('suppliers.edit','suppliers','עריכת ספקים','עדכון פרטי ספק',30,true,false,true),
        ('suppliers.deactivate','suppliers','השבתת ספקים','השבתת ספק',40,true,false,true),
        ('suppliers.restore','suppliers','שחזור ספקים','שחזור ספק שהושבת',50,true,false,true),
        ('suppliers.export','suppliers','ייצוא ספקים','ייצוא נתוני ספקים',60,true,false,true),
        ('assistance_types.view','assistance_types','צפייה בסוגי סיוע','צפייה ברשימת סוגי סיוע',10,true,false,true),
        ('assistance_types.create','assistance_types','יצירת סוגי סיוע','הוספת סוג סיוע חדש',20,true,false,false),
        ('assistance_types.edit','assistance_types','עריכת סוגי סיוע','עדכון סוג סיוע',30,true,false,true),
        ('assistance_types.deactivate','assistance_types','השבתת סוגי סיוע','השבתת סוג סיוע',40,true,false,true),
        ('assistance_types.restore','assistance_types','שחזור סוגי סיוע','שחזור סוג סיוע שהושבת',50,true,false,true),
        ('committee_decisions.view','committee_decisions','צפייה בהחלטות ועדה','צפייה בהחלטות ועדה',10,true,true,true),
        ('committee_decisions.create','committee_decisions','יצירת החלטת ועדה','פתיחת החלטת ועדה חדשה',20,true,false,false),
        ('committee_decisions.edit_draft','committee_decisions','עריכת טיוטת החלטה','עריכת החלטה במצב טיוטה',30,true,true,true),
        ('committee_decisions.submit','committee_decisions','הגשת החלטה לועדה','שליחת טיוטה לאישור',40,true,true,true),
        ('committee_decisions.approve','committee_decisions','אישור החלטת ועדה','אישור החלטה — organization scope only',50,true,true,true),
        ('committee_decisions.reject','committee_decisions','דחיית החלטת ועדה','דחיית החלטה — organization scope only',60,true,true,true),
        ('committee_decisions.cancel','committee_decisions','ביטול החלטת ועדה','ביטול החלטת ועדה',70,true,true,true),
        ('assistance_items.view','assistance_items','צפייה בפריטי סיוע','צפייה בפריטי סיוע בהחלטה',10,true,true,true),
        ('assistance_items.create','assistance_items','הוספת פריט סיוע','הוספת פריט סיוע לטיוטה',20,true,false,false),
        ('assistance_items.edit','assistance_items','עריכת פריט סיוע','עריכת פריט סיוע בטיוטה',30,true,true,true),
        ('assistance_items.remove_draft','assistance_items','הסרת פריט מטיוטה','הסרת פריט סיוע מטיוטה',40,true,true,true)
        ON CONFLICT (permission_key) DO UPDATE SET
            category = EXCLUDED.category,
            display_name_he = EXCLUDED.display_name_he,
            description_he = EXCLUDED.description_he,
            sort_order = EXCLUDED.sort_order,
            is_active = true,
            supports_my_records = EXCLUDED.supports_my_records,
            scope_applies = EXCLUDED.scope_applies;
        """;
}
