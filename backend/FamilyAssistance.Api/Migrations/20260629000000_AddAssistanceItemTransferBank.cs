using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class AddAssistanceItemTransferBank : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "transfer_account_number",
            table: "assistance_items",
            type: "character varying(34)",
            maxLength: 34,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "transfer_bank_number",
            table: "assistance_items",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "transfer_branch_number",
            table: "assistance_items",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "transfer_account_number",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "transfer_bank_number",
            table: "assistance_items");

        migrationBuilder.DropColumn(
            name: "transfer_branch_number",
            table: "assistance_items");
    }
}
