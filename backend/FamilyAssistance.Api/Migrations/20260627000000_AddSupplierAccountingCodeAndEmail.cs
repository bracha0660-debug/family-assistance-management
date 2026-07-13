using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class AddSupplierAccountingCodeAndEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "accounting_code",
            table: "suppliers",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "email",
            table: "suppliers",
            type: "character varying(254)",
            maxLength: 254,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "accounting_code",
            table: "suppliers");

        migrationBuilder.DropColumn(
            name: "email",
            table: "suppliers");
    }
}
