using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

/// <summary>
/// Phase 14 M76 — AssistanceItem.Status workflow field + ApprovedAt, with backfill from decision/payment state.
/// </summary>
public partial class AssistanceItemWorkflowStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "assistance_items",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "approved_at",
            table: "assistance_items",
            type: "timestamp with time zone",
            nullable: true);

        // Backfill from parent decision + payment execution (audit mapping per organization).
        migrationBuilder.Sql("""
            UPDATE assistance_items ai
            SET status = mapped.new_status
            FROM (
                SELECT ai2.id,
                    CASE
                        WHEN cd.status = 'draft' THEN 'draft'
                        WHEN cd.status = 'submitted' THEN 'submitted'
                        WHEN cd.status = 'returned_for_revision' THEN 'returned'
                        WHEN cd.status = 'rejected' THEN 'rejected'
                        WHEN cd.status = 'suspended' THEN 'suspended'
                        WHEN pe.status = 'paid' THEN 'paid'
                        WHEN cd.status = 'approved'
                             AND pe.status IN ('awaiting_payment', 'executing', 'proof_uploaded') THEN 'waiting_for_reference'
                        WHEN cd.status IN ('approved', 'partially_paid', 'fully_paid') AND pe.id IS NULL THEN 'approved'
                        WHEN cd.status IN ('approved', 'partially_paid') THEN 'waiting_for_reference'
                        WHEN cd.status = 'fully_paid' THEN 'paid'
                        ELSE 'draft'
                    END AS new_status
                FROM assistance_items ai2
                INNER JOIN committee_decisions cd ON ai2.committee_decision_id = cd.id
                LEFT JOIN payment_executions pe ON pe.assistance_item_id = ai2.id
            ) mapped
            WHERE ai.id = mapped.id;

            DO $$
            DECLARE
                r RECORD;
            BEGIN
                FOR r IN
                    SELECT organization_id, status, COUNT(*) AS cnt
                    FROM assistance_items
                    WHERE status IS NOT NULL
                    GROUP BY organization_id, status
                    ORDER BY organization_id, status
                LOOP
                    RAISE NOTICE 'phase14_m76_backfill org=% status=% count=%',
                        r.organization_id, r.status, r.cnt;
                END LOOP;
            END $$;
            """);

        migrationBuilder.Sql("""
            UPDATE assistance_items SET status = 'draft' WHERE status IS NULL;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "status",
            table: "assistance_items",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "draft");

        migrationBuilder.CreateIndex(
            name: "ix_assistance_items_org_status",
            table: "assistance_items",
            columns: new[] { "organization_id", "status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_assistance_items_org_status",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "approved_at",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "status",
            table: "assistance_items");
    }
}
