using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Assets.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssetDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assets");

            migrationBuilder.CreateTable(
                name: "asset_statuses",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("pk_asset_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_types",
                schema: "assets",
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
                    table.PrimaryKey("pk_asset_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    normalized_asset_tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    serial_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    normalized_serial_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    manufacturer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    normalized_manufacturer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    asset_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: true),
                    warranty_expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    vendor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),

                    // The generated `xmin = table.Column<uint>(type: "xid", ...)` line was
                    // deleted from here before this migration was ever applied. PostgreSQL
                    // refuses CREATE TABLE with a user column called xmin — it is the
                    // system row-version column every table already has, which is the whole
                    // reason AssetConfiguration maps it as the concurrency token.
                    //
                    // Npgsql 10 dropped UseXminAsConcurrencyToken() and no longer suppresses
                    // the column when scaffolding, so `dotnet ef migrations add` will emit
                    // that line again for any future migration touching this table; delete
                    // it again. The model snapshot deliberately KEEPS the property, which is
                    // what stops a later migration trying to add the column for real.
                    //
                    // WP-1.2 hit this first on helpdesk.tickets and its migration carries
                    // the same note.
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_assets_asset_status_id",
                        column: x => x.asset_status_id,
                        principalSchema: "assets",
                        principalTable: "asset_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assets_asset_type_id",
                        column: x => x.asset_type_id,
                        principalSchema: "assets",
                        principalTable: "asset_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asset_statuses_active_order",
                schema: "assets",
                table: "asset_statuses",
                columns: new[] { "is_active", "sort_order", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ux_asset_statuses_code",
                schema: "assets",
                table: "asset_statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_asset_statuses_normalized_name",
                schema: "assets",
                table: "asset_statuses",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_types_active_order",
                schema: "assets",
                table: "asset_types",
                columns: new[] { "is_active", "sort_order", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ux_asset_types_normalized_name",
                schema: "assets",
                table: "asset_types",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_asset_type_id",
                schema: "assets",
                table: "assets",
                column: "asset_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_assigned_to_user_id",
                schema: "assets",
                table: "assets",
                column: "assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_department_id",
                schema: "assets",
                table: "assets",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_location_id",
                schema: "assets",
                table: "assets",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_status_type",
                schema: "assets",
                table: "assets",
                columns: new[] { "asset_status_id", "asset_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_warranty_expires_at",
                schema: "assets",
                table: "assets",
                column: "warranty_expires_at");

            migrationBuilder.CreateIndex(
                name: "ux_assets_manufacturer_serial_number",
                schema: "assets",
                table: "assets",
                columns: new[] { "normalized_manufacturer", "normalized_serial_number" },
                unique: true,
                filter: "\"normalized_manufacturer\" IS NOT NULL AND \"normalized_serial_number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_assets_normalized_asset_tag",
                schema: "assets",
                table: "assets",
                column: "normalized_asset_tag",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assets",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_statuses",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_types",
                schema: "assets");
        }
    }
}
