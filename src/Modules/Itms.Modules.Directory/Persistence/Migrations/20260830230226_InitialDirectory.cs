using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itms.Modules.Directory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "directory");

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "directory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    normalized_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "directory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    full_path = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_locations_parent",
                        column: x => x.parent_id,
                        principalSchema: "directory",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_departments_active_name",
                schema: "directory",
                table: "departments",
                columns: new[] { "is_active", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ux_departments_normalized_code",
                schema: "directory",
                table: "departments",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_departments_normalized_name",
                schema: "directory",
                table: "departments",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locations_full_path",
                schema: "directory",
                table: "locations",
                column: "full_path");

            migrationBuilder.CreateIndex(
                name: "ix_locations_parent",
                schema: "directory",
                table: "locations",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_locations_path",
                schema: "directory",
                table: "locations",
                column: "path")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "ux_locations_parent_name",
                schema: "directory",
                table: "locations",
                columns: new[] { "parent_id", "normalized_name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "departments",
                schema: "directory");

            migrationBuilder.DropTable(
                name: "locations",
                schema: "directory");
        }
    }
}
