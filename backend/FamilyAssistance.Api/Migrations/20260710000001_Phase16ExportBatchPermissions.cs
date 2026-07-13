using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

/// <summary>
/// Phase 16 M93 — export-batch permission catalog keys + Finance preset grants.
/// C10: send_to_execution aligns to payments.export_batches.create; payments.execute remains legacy proof-path.
/// </summary>
public partial class Phase16ExportBatchPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO permission_catalog (permission_key, category, display_name_he, description_he, sort_order, is_active, supports_my_records, scope_applies)
            VALUES
              ('payments.export_batches.create', 'payments', 'יצירת גליון ייצוא', 'בחירת פריטים מאושרים והעברה לביצוע — יצירת ExportBatch', 70, true, false, true),
              ('payments.export_batches.download', 'payments', 'הורדת גליון ייצוא', 'הורדה / הורדה מחדש של גליון ייצוא קיים — נפרד מיצירה', 80, true, false, true),
              ('payments.export_batches.cancel', 'payments', 'ביטול גליון ייצוא', 'ביטול רך של גליון ייצוא שלם', 90, true, false, true),
              ('payments.export_batch_items.cancel', 'payments', 'ביטול ייצוא לפריט', 'ביטול רך של פריט בודד בגליון ייצוא', 100, true, false, true),
              ('payments.edit_assistance_items', 'payments', 'עריכת פרטי פריט בתשלומים', 'עריכת פרטי סיוע/תשלום במסך תשלומים — נפרד מצפייה ומפעולות ייצוא', 110, true, false, true)
            ON CONFLICT (permission_key) DO UPDATE SET
              category = EXCLUDED.category,
              display_name_he = EXCLUDED.display_name_he,
              description_he = EXCLUDED.description_he,
              sort_order = EXCLUDED.sort_order,
              is_active = true,
              supports_my_records = EXCLUDED.supports_my_records,
              scope_applies = EXCLUDED.scope_applies;

            UPDATE permission_catalog
            SET display_name_he = 'סיום תהליך פריט',
                description_he = 'סיים תהליך — סגירת פריט ששולם (נסגר) — organization scope only'
            WHERE permission_key = 'assistance_items.complete';

            UPDATE permission_catalog
            SET display_name_he = 'ביצוע תשלום (מסלול ישן)',
                description_he = 'מסלול הוכחת תשלום ישן בלבד — לא ליצירת גליון ייצוא'
            WHERE permission_key = 'payments.execute';

            UPDATE permission_catalog
            SET description_he = 'צפייה בתור תשלומים, בסטטוס ביצוע ובפרטי תשלום — לא מעניק עריכת פרטי פריט'
            WHERE permission_key = 'payments.view';

            INSERT INTO organization_role_grants (organization_role_id, permission_key, scope, granted_at)
            SELECT r.id, g.permission_key, g.scope, NOW()
            FROM organization_roles r
            CROSS JOIN (VALUES
              ('payments.export_batches.create', 'organization'),
              ('payments.export_batches.download', 'organization'),
              ('payments.export_batches.cancel', 'organization'),
              ('payments.export_batch_items.cancel', 'organization'),
              ('payments.edit_assistance_items', 'organization')
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
            WHERE permission_key IN (
              'payments.export_batches.create',
              'payments.export_batches.download',
              'payments.export_batches.cancel',
              'payments.export_batch_items.cancel',
              'payments.edit_assistance_items'
            );

            DELETE FROM permission_catalog
            WHERE permission_key IN (
              'payments.export_batches.create',
              'payments.export_batches.download',
              'payments.export_batches.cancel',
              'payments.export_batch_items.cancel',
              'payments.edit_assistance_items'
            );
            """);
    }
}
