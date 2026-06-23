using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class WorkflowCompletionPhase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "approval_notes",
            table: "committee_decisions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "pre_suspend_status",
            table: "committee_decisions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "resumed_at",
            table: "committee_decisions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "pre_hold_status",
            table: "payment_executions",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "approval_notes", table: "committee_decisions");
        migrationBuilder.DropColumn(name: "pre_suspend_status", table: "committee_decisions");
        migrationBuilder.DropColumn(name: "resumed_at", table: "committee_decisions");
        migrationBuilder.DropColumn(name: "pre_hold_status", table: "payment_executions");
    }
}
