using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyAssistance.Api.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

        migrationBuilder.CreateTable(
            name: "organizations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_organizations", x => x.id));

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.id);
                table.ForeignKey(
                    name: "FK_users_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "user_sessions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ip_address = table.Column<string>(type: "text", nullable: true),
                user_agent = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_sessions", x => x.id);
                table.ForeignKey(
                    name: "FK_user_sessions_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "bank_accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                owner_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                bank_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                branch_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                account_number = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bank_accounts", x => x.id);
                table.ForeignKey(
                    name: "FK_bank_accounts_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                event_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                old_value = table.Column<string>(type: "text", nullable: true),
                new_value = table.Column<string>(type: "text", nullable: true),
                reason = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_logs", x => x.id);
                table.ForeignKey(
                    name: "FK_audit_logs_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "FK_audit_logs_users_actor_user_id",
                    column: x => x.actor_user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "security_audit_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                event_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                username_attempted = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: true),
                organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                session_id = table.Column<Guid>(type: "uuid", nullable: true),
                ip_address = table.Column<string>(type: "text", nullable: true),
                user_agent = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_security_audit_logs", x => x.id);
                table.ForeignKey(
                    name: "FK_security_audit_logs_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "FK_security_audit_logs_user_sessions_session_id",
                    column: x => x.session_id,
                    principalTable: "user_sessions",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "FK_security_audit_logs_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "bank_account_history",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                change_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                bank_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                branch_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                account_number = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                reason = table.Column<string>(type: "text", nullable: true),
                changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                old_values = table.Column<string>(type: "jsonb", nullable: true),
                new_values = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bank_account_history", x => x.id);
                table.ForeignKey(
                    name: "FK_bank_account_history_bank_accounts_bank_account_id",
                    column: x => x.bank_account_id,
                    principalTable: "bank_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_bank_account_history_organizations_organization_id",
                    column: x => x.organization_id,
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_bank_account_history_users_changed_by_user_id",
                    column: x => x.changed_by_user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "ux_organizations_code", table: "organizations", column: "code", unique: true);
        migrationBuilder.CreateIndex(name: "ix_organizations_status", table: "organizations", column: "status");
        migrationBuilder.CreateIndex(name: "ux_users_username", table: "users", column: "username", unique: true);
        migrationBuilder.CreateIndex(name: "ix_users_org_status", table: "users", columns: new[] { "organization_id", "status" });
        migrationBuilder.CreateIndex(name: "ix_users_org_role", table: "users", columns: new[] { "organization_id", "role" });
        migrationBuilder.CreateIndex(name: "ux_sessions_token_hash", table: "user_sessions", column: "session_token_hash", unique: true);
        migrationBuilder.CreateIndex(name: "ix_sessions_user_active", table: "user_sessions", columns: new[] { "user_id", "revoked_at", "expires_at" });

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX ux_bank_accounts_active_owner
                ON bank_accounts (organization_id, owner_entity_type, owner_entity_id)
                WHERE is_active = true;
            CREATE UNIQUE INDEX ux_bank_accounts_org_full_identity
                ON bank_accounts (organization_id, bank_number, branch_number, account_number)
                WHERE is_active = true;
            """);

        migrationBuilder.CreateIndex(name: "ix_bank_history_account", table: "bank_account_history", columns: new[] { "bank_account_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_bank_history_org", table: "bank_account_history", columns: new[] { "organization_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_audit_org_time", table: "audit_logs", columns: new[] { "organization_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_audit_entity", table: "audit_logs", columns: new[] { "entity_type", "entity_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_audit_actor", table: "audit_logs", columns: new[] { "actor_user_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_audit_event_code", table: "audit_logs", columns: new[] { "event_code", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_security_audit_time", table: "security_audit_logs", column: "created_at");
        migrationBuilder.CreateIndex(name: "ix_security_audit_username", table: "security_audit_logs", columns: new[] { "username_attempted", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_security_audit_user", table: "security_audit_logs", columns: new[] { "user_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_security_audit_event", table: "security_audit_logs", columns: new[] { "event_code", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bank_account_history");
        migrationBuilder.DropTable(name: "security_audit_logs");
        migrationBuilder.DropTable(name: "audit_logs");
        migrationBuilder.DropTable(name: "bank_accounts");
        migrationBuilder.DropTable(name: "user_sessions");
        migrationBuilder.DropTable(name: "users");
        migrationBuilder.DropTable(name: "organizations");
    }
}
