using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Monitoring.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MonitoringDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "monitoring");

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "monitoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    normalized_hostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    monitoring_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    poll_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    failure_threshold = table.Column<int>(type: "integer", nullable: false),
                    snmp_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    snmp_port = table.Column<int>(type: "integer", nullable: false),
                    snmp_community = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),

                    // The generated `xmin = table.Column<uint>(type: "xid", ...)` line was
                    // deleted from here before this migration was ever applied. PostgreSQL
                    // refuses CREATE TABLE with a user column called xmin — it is the
                    // system row-version column every table already has, which is the whole
                    // reason MonitoredDeviceConfiguration maps it as the concurrency token.
                    //
                    // Npgsql 10 dropped UseXminAsConcurrencyToken() and no longer suppresses
                    // the column when scaffolding, so `dotnet ef migrations add` will emit
                    // that line again for any future migration touching this table; delete
                    // it again. The model snapshot deliberately KEEPS the property, which is
                    // what stops a later migration trying to add the column for real.
                    //
                    // WP-1.2 hit this first on helpdesk.tickets and WP-2.1 on assets.assets;
                    // both migrations carry the same note.
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "check_results",
                schema: "monitoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_check_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_check_results_device_id",
                        column: x => x.device_id,
                        principalSchema: "monitoring",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_check_results_device_id_checked_at",
                schema: "monitoring",
                table: "check_results",
                columns: new[] { "device_id", "checked_at" })
                .Annotation("Npgsql:IndexMethod", "brin");

            migrationBuilder.CreateIndex(
                name: "ix_devices_monitoring_enabled_poll_interval",
                schema: "monitoring",
                table: "devices",
                columns: new[] { "monitoring_enabled", "poll_interval_seconds" });

            migrationBuilder.CreateIndex(
                name: "ix_devices_normalized_hostname",
                schema: "monitoring",
                table: "devices",
                column: "normalized_hostname");

            migrationBuilder.CreateIndex(
                name: "ux_devices_asset_id",
                schema: "monitoring",
                table: "devices",
                column: "asset_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "check_results",
                schema: "monitoring");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "monitoring");
        }
    }
}
