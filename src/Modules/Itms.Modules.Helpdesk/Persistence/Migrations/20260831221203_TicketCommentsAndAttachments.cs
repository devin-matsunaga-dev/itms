using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Helpdesk.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketCommentsAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_attachments",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    stored_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_attachments_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "helpdesk",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_comments",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_comments_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "helpdesk",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachments_ticket_id_created_at",
                schema: "helpdesk",
                table: "ticket_attachments",
                columns: new[] { "ticket_id", "is_internal", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_ticket_attachments_stored_name",
                schema: "helpdesk",
                table: "ticket_attachments",
                column: "stored_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_comments_ticket_id_created_at",
                schema: "helpdesk",
                table: "ticket_comments",
                columns: new[] { "ticket_id", "is_internal", "created_at" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_attachments",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "ticket_comments",
                schema: "helpdesk");
        }
    }
}
