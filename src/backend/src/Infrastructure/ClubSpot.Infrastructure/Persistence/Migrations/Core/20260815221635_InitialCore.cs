using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations.Core
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "club",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    venue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    time_zone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    deposit_percent = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club", x => x.id);
                    table.CheckConstraint("ck_club_deposit_percent", "deposit_percent BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateIndex(
                name: "ux_club_slug",
                schema: "core",
                table: "club",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "club",
                schema: "core");
        }
    }
}
