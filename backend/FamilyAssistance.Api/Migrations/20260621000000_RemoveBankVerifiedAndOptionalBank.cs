using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class RemoveBankVerifiedAndOptionalBank : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "bank_verified_externally", table: "families");
        migrationBuilder.DropColumn(name: "bank_verified_externally", table: "suppliers");

        migrationBuilder.Sql("""
            UPDATE families
            SET bank_number = NULLIF(TRIM(bank_number), ''),
                branch_number = NULLIF(TRIM(branch_number), ''),
                account_number = NULLIF(TRIM(account_number), ''),
                account_holder_name = NULLIF(TRIM(account_holder_name), '');
            """);

        migrationBuilder.Sql("""
            UPDATE suppliers
            SET bank_number = NULLIF(TRIM(bank_number), ''),
                branch_number = NULLIF(TRIM(branch_number), ''),
                account_number = NULLIF(TRIM(account_number), ''),
                account_holder_name = NULLIF(TRIM(account_holder_name), '');
            """);

        migrationBuilder.AlterColumn<string>(
            name: "bank_number",
            table: "families",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10);

        migrationBuilder.AlterColumn<string>(
            name: "branch_number",
            table: "families",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10);

        migrationBuilder.AlterColumn<string>(
            name: "account_number",
            table: "families",
            type: "character varying(34)",
            maxLength: 34,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(34)",
            oldMaxLength: 34);

        migrationBuilder.AlterColumn<string>(
            name: "account_holder_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200);

        migrationBuilder.AlterColumn<string>(
            name: "bank_number",
            table: "suppliers",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10);

        migrationBuilder.AlterColumn<string>(
            name: "branch_number",
            table: "suppliers",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10);

        migrationBuilder.AlterColumn<string>(
            name: "account_number",
            table: "suppliers",
            type: "character varying(34)",
            maxLength: 34,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(34)",
            oldMaxLength: 34);

        migrationBuilder.AlterColumn<string>(
            name: "account_holder_name",
            table: "suppliers",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE families
            SET bank_number = COALESCE(bank_number, ''),
                branch_number = COALESCE(branch_number, ''),
                account_number = COALESCE(account_number, ''),
                account_holder_name = COALESCE(account_holder_name, '');
            """);

        migrationBuilder.Sql("""
            UPDATE suppliers
            SET bank_number = COALESCE(bank_number, ''),
                branch_number = COALESCE(branch_number, ''),
                account_number = COALESCE(account_number, ''),
                account_holder_name = COALESCE(account_holder_name, '');
            """);

        migrationBuilder.AlterColumn<string>(
            name: "bank_number",
            table: "families",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "branch_number",
            table: "families",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "account_number",
            table: "families",
            type: "character varying(34)",
            maxLength: 34,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(34)",
            oldMaxLength: 34,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "account_holder_name",
            table: "families",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "bank_number",
            table: "suppliers",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "branch_number",
            table: "suppliers",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(10)",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "account_number",
            table: "suppliers",
            type: "character varying(34)",
            maxLength: 34,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(34)",
            oldMaxLength: 34,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "account_holder_name",
            table: "suppliers",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "bank_verified_externally",
            table: "families",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "bank_verified_externally",
            table: "suppliers",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }
}
