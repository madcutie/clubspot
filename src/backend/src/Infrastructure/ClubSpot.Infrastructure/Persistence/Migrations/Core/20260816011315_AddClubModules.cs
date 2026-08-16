using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations.Core
{
    /// <inheritdoc />
    public partial class AddClubModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "club_module",
                schema: "core",
                columns: table => new
                {
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contracted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_module", x => new { x.club_id, x.module_id });
                    table.ForeignKey(
                        name: "FK_club_module_club_club_id",
                        column: x => x.club_id,
                        principalSchema: "core",
                        principalTable: "club",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_club_module_club_id",
                schema: "core",
                table: "club_module",
                column: "club_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "club_module",
                schema: "core");
        }
    }
}
