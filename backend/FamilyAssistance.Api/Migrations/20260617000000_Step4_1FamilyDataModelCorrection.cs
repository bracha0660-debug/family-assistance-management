using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class Step4_1FamilyDataModelCorrection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "accounting_number_counter",
            table: "organizations",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "external_accounting_number",
            table: "families",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "family_last_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "father_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "father_israeli_id",
            table: "families",
            type: "character varying(9)",
            maxLength: 9,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "mother_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "mother_israeli_id",
            table: "families",
            type: "character varying(9)",
            maxLength: 9,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "number_of_children",
            table: "families",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql("""
            WITH numbered AS (
                SELECT id,
                       ROW_NUMBER() OVER (PARTITION BY organization_id ORDER BY created_at, id) AS seq
                FROM families
            )
            UPDATE families f
            SET family_last_name = COALESCE(NULLIF(TRIM(f.head_of_household_name), ''), 'לא ידוע'),
                father_israeli_id = NULLIF(TRIM(f.head_id_number), ''),
                number_of_children = COALESCE(f.household_size, 0),
                external_accounting_number = n.seq
            FROM numbered n
            WHERE f.id = n.id;
            """);

        migrationBuilder.Sql("""
            UPDATE organizations o
            SET accounting_number_counter = COALESCE(sub.max_num, 0)
            FROM (
                SELECT organization_id, MAX(external_accounting_number) AS max_num
                FROM families
                GROUP BY organization_id
            ) sub
            WHERE o.id = sub.organization_id;
            """);

        migrationBuilder.AlterColumn<long>(
            name: "external_accounting_number",
            table: "families",
            type: "bigint",
            nullable: false,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "family_last_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "number_of_children",
            table: "families",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.DropColumn(name: "head_of_household_name", table: "families");
        migrationBuilder.DropColumn(name: "head_id_number", table: "families");
        migrationBuilder.DropColumn(name: "household_size", table: "families");

        migrationBuilder.CreateIndex(
            name: "ux_families_org_accounting_number",
            table: "families",
            columns: new[] { "organization_id", "external_accounting_number" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ux_families_org_accounting_number", table: "families");

        migrationBuilder.AddColumn<string>(
            name: "head_of_household_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "head_id_number",
            table: "families",
            type: "character varying(9)",
            maxLength: 9,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "household_size",
            table: "families",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("""
            UPDATE families
            SET head_of_household_name = family_last_name,
                head_id_number = father_israeli_id,
                household_size = number_of_children;
            """);

        migrationBuilder.DropColumn(name: "external_accounting_number", table: "families");
        migrationBuilder.DropColumn(name: "family_last_name", table: "families");
        migrationBuilder.DropColumn(name: "father_name", table: "families");
        migrationBuilder.DropColumn(name: "father_israeli_id", table: "families");
        migrationBuilder.DropColumn(name: "mother_name", table: "families");
        migrationBuilder.DropColumn(name: "mother_israeli_id", table: "families");
        migrationBuilder.DropColumn(name: "number_of_children", table: "families");
        migrationBuilder.DropColumn(name: "accounting_number_counter", table: "organizations");
    }
}
