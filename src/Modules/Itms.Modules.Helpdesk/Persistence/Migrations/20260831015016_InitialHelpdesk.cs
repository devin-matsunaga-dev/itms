using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Helpdesk.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialHelpdesk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "helpdesk");

            migrationBuilder.CreateTable(
                name: "ticket_categories",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_priorities",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    response_target_minutes = table.Column<int>(type: "integer", nullable: false),
                    resolution_target_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_priorities", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_categories_active_order",
                schema: "helpdesk",
                table: "ticket_categories",
                columns: new[] { "is_active", "sort_order", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ux_ticket_categories_normalized_name",
                schema: "helpdesk",
                table: "ticket_categories",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_priorities_active_rank",
                schema: "helpdesk",
                table: "ticket_priorities",
                columns: new[] { "is_active", "rank", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ux_ticket_priorities_code",
                schema: "helpdesk",
                table: "ticket_priorities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ticket_priorities_normalized_name",
                schema: "helpdesk",
                table: "ticket_priorities",
                column: "normalized_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_categories",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "ticket_priorities",
                schema: "helpdesk");
        }
    }
}
