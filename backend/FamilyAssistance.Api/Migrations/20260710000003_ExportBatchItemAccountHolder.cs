using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

/// <summary>Phase 16 M95 — snapshot account holder on export batch items for bank details in sheet.</summary>
public partial class ExportBatchItemAccountHolder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "account_holder_name",
            table: "export_batch_items",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "account_holder_name",
            table: "export_batch_items");
    }
}
