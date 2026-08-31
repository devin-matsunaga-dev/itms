using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Helpdesk.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_number_sequences",
                schema: "helpdesk",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    next_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_number_sequences", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    related_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_alert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_notes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)

                    // The model's concurrency token maps to xmin, which PostgreSQL already
                    // gives every row. Declaring it here would be rejected outright
                    // ("column name \"xmin\" conflicts with a system column name"), so the
                    // generated line was removed by hand before this migration was ever
                    // applied. Npgsql 10 dropped the UseXminAsConcurrencyToken() helper
                    // that used to do this; the model snapshot still carries the property,
                    // so no later migration will try to add the column back.
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_tickets_category_id",
                        column: x => x.category_id,
                        principalSchema: "helpdesk",
                        principalTable: "ticket_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_priority_id",
                        column: x => x.priority_id,
                        principalSchema: "helpdesk",
                        principalTable: "ticket_priorities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_assignee_status",
                schema: "helpdesk",
                table: "tickets",
                columns: new[] { "assignee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_category_id",
                schema: "helpdesk",
                table: "tickets",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_department_id",
                schema: "helpdesk",
                table: "tickets",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_priority_id",
                schema: "helpdesk",
                table: "tickets",
                column: "priority_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_requester_status",
                schema: "helpdesk",
                table: "tickets",
                columns: new[] { "requester_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_status_created_at",
                schema: "helpdesk",
                table: "tickets",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_tickets_number",
                schema: "helpdesk",
                table: "tickets",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_number_sequences",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "helpdesk");
        }
    }
}
