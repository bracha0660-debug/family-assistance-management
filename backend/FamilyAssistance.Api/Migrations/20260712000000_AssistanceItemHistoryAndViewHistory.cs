using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

/// <summary>
/// Phase 16 B — AssistanceItem parent/child history + account_holder_name + view_history permission.
/// </summary>
public partial class AssistanceItemHistoryAndViewHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "account_holder_name",
            table: "assistance_items",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "assistance_item_history_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                assistance_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                event_description_he = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                related_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_assistance_item_history_events", x => x.id);
                table.ForeignKey(
                    name: "fk_assistance_item_history_events_assistance_items_assistance_i",
                    column: x => x.assistance_item_id,
                    principalTable: "assistance_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_assistance_item_history_events_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_assistance_item_history_events_users_actor_user_id",
                    column: x => x.actor_user_id,
                    principalTable: "users",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "assistance_item_history_field_changes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                history_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                field_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                field_label_he = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                previous_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                new_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                is_sensitive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_assistance_item_history_field_changes", x => x.id);
                table.ForeignKey(
                    name: "fk_assistance_item_history_field_changes_assistance_item_histor",
                    column: x => x.history_event_id,
                    principalTable: "assistance_item_history_events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_ai_history_events_item_time_id",
            table: "assistance_item_history_events",
            columns: new[] { "assistance_item_id", "occurred_at", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_ai_history_events_org_item_time",
            table: "assistance_item_history_events",
            columns: new[] { "organization_id", "assistance_item_id", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "ix_assistance_item_history_events_actor_user_id",
            table: "assistance_item_history_events",
            column: "actor_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_ai_history_field_changes_event",
            table: "assistance_item_history_field_changes",
            column: "history_event_id");

        migrationBuilder.Sql("""
            INSERT INTO permission_catalog (permission_key, category, display_name_he, description_he, sort_order, is_active, supports_my_records, scope_applies)
            VALUES
              ('assistance_items.view_history', 'assistance_items', 'צפייה בהיסטוריית פריט', 'צפייה בחלון היסטוריית פריט סיוע — לא מוענק אוטומטית מצפייה או עריכה', 60, true, true, true)
            ON CONFLICT (permission_key) DO UPDATE SET
              category = EXCLUDED.category,
              display_name_he = EXCLUDED.display_name_he,
              description_he = EXCLUDED.description_he,
              sort_order = EXCLUDED.sort_order,
              is_active = true,
              supports_my_records = EXCLUDED.supports_my_records,
              scope_applies = EXCLUDED.scope_applies;

            UPDATE permission_catalog
            SET description_he = 'סיים תהליך — סגירת פריט ששולם (תהליך הושלם) — organization scope only'
            WHERE permission_key = 'assistance_items.complete';

            INSERT INTO organization_role_grants (organization_role_id, permission_key, scope, granted_at)
            SELECT r.id, g.permission_key, g.scope, NOW()
            FROM organization_roles r
            CROSS JOIN (VALUES
              ('assistance_items.view_history', 'my_records')
            ) AS g(permission_key, scope)
            WHERE r.factory_preset_key = 'preset_coordinator'
              AND NOT EXISTS (
                SELECT 1 FROM organization_role_grants existing
                WHERE existing.organization_role_id = r.id
                  AND existing.permission_key = g.permission_key
              );

            INSERT INTO organization_role_grants (organization_role_id, permission_key, scope, granted_at)
            SELECT r.id, g.permission_key, g.scope, NOW()
            FROM organization_roles r
            CROSS JOIN (VALUES
              ('assistance_items.view_history', 'organization')
            ) AS g(permission_key, scope)
            WHERE r.factory_preset_key IN ('preset_manager', 'preset_finance')
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
            WHERE permission_key = 'assistance_items.view_history';

            DELETE FROM permission_catalog
            WHERE permission_key = 'assistance_items.view_history';
            """);

        migrationBuilder.DropTable(name: "assistance_item_history_field_changes");
        migrationBuilder.DropTable(name: "assistance_item_history_events");
        migrationBuilder.DropColumn(name: "account_holder_name", table: "assistance_items");
    }
}
