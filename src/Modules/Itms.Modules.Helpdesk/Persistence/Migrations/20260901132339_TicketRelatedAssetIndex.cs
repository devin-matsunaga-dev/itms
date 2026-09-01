using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Helpdesk.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketRelatedAssetIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_tickets_related_asset_id",
                schema: "helpdesk",
                table: "tickets",
                column: "related_asset_id",
                filter: "related_asset_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_related_asset_id",
                schema: "helpdesk",
                table: "tickets");
        }
    }
}
