using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class AddAssistanceTypeSuppliers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assistance_type_suppliers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                assistance_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assistance_type_suppliers", x => x.id);
                table.ForeignKey(
                    name: "FK_assistance_type_suppliers_assistance_types_assistance_type_id",
                    column: x => x.assistance_type_id,
                    principalTable: "assistance_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_assistance_type_suppliers_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_assistance_type_suppliers_suppliers_supplier_id",
                    column: x => x.supplier_id,
                    principalTable: "suppliers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_assistance_type_suppliers_org_type",
            table: "assistance_type_suppliers",
            columns: new[] { "organization_id", "assistance_type_id" });

        migrationBuilder.CreateIndex(
            name: "ux_assistance_type_suppliers_org_type_supplier",
            table: "assistance_type_suppliers",
            columns: new[] { "organization_id", "assistance_type_id", "supplier_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "assistance_type_suppliers");
    }
}
