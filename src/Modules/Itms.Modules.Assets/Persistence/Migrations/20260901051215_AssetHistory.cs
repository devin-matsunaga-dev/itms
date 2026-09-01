using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Assets.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssetHistory : Migration
    {
        /// <inheritdoc />
        // No xmin hand-edit was needed here, unlike 20260901043937_AssetDomain: this
        // migration creates a table and does not touch assets.assets, so the provider had
        // no concurrency token to scaffold. The note in that migration still stands for any
        // future one that does alter the assets table — the `xmin = table.Column<uint>(...)`
        // line will be emitted again and must be deleted again before it is applied.
        //
        // The history table has no row version of its own on purpose: entries are written
        // once and never updated, so there is nothing to race.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asset_history",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    from_value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    to_value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_history_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asset_history_asset_id_occurred_at",
                schema: "assets",
                table: "asset_history",
                columns: new[] { "asset_id", "occurred_at", "sequence" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_history",
                schema: "assets");
        }
    }
}
