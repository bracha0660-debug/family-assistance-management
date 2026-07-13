using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class AddSupplierActiveRegistrationUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ux_suppliers_org_active_registration",
            table: "suppliers",
            columns: new[] { "organization_id", "registration_number" },
            unique: true,
            filter: "status = 'active' AND registration_number IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_suppliers_org_active_registration",
            table: "suppliers");
    }
}
