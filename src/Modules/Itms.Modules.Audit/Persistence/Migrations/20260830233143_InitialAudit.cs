using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Audit.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    source_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_actor",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "actor_id", "occurred_at" },
                filter: "actor_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_entity",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_occurred_at",
                schema: "audit",
                table: "audit_entries",
                column: "occurred_at");

            // Invariant 10: an audit entry is never modified or deleted through any code
            // path in this system. The module offers none — there is no DbSet to call
            // Remove or ExecuteUpdate on, and a test asserts no such call exists anywhere
            // in the solution — but the guarantee is worth more than the code that
            // implements it, so the database refuses as well. This holds against a
            // hand-written UPDATE in psql, which no amount of C# discipline would.
            //
            // TRUNCATE is deliberately not covered: it needs table ownership rather than
            // write access, and the integration suite's between-test reset relies on it.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.reject_audit_entry_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION
                        'audit.audit_entries is append-only; % is not permitted.', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER trg_audit_entries_append_only
                    BEFORE UPDATE OR DELETE ON audit.audit_entries
                    FOR EACH ROW EXECUTE FUNCTION audit.reject_audit_entry_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table takes its trigger with it; the function is schema-level
            // and has to go on its own.
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "audit");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit.reject_audit_entry_mutation();");
        }
    }
}
