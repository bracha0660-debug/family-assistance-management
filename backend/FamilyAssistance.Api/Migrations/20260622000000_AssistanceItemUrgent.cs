using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class AssistanceItemUrgent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_urgent",
            table: "assistance_items",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""
            UPDATE assistance_items ai
            SET is_urgent = cd.is_urgent
            FROM committee_decisions cd
            WHERE ai.committee_decision_id = cd.id
              AND cd.is_urgent = true;
            """);

        migrationBuilder.DropColumn(name: "is_urgent", table: "committee_decisions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_urgent",
            table: "committee_decisions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""
            UPDATE committee_decisions cd
            SET is_urgent = EXISTS (
                SELECT 1 FROM assistance_items ai
                WHERE ai.committee_decision_id = cd.id AND ai.is_urgent = true
            );
            """);

        migrationBuilder.DropColumn(name: "is_urgent", table: "assistance_items");
    }
}
