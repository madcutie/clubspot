using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations.Bookings
{
    /// <inheritdoc />
    public partial class InitialBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bookings");

            migrationBuilder.CreateTable(
                name: "schedule",
                schema: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    timeZone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    weeklyRanges = table.Column<string>(type: "jsonb", nullable: false),
                    specialDates = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "court",
                schema: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    sport = table.Column<string>(type: "text", nullable: false),
                    sortOrder = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    isCovered = table.Column<bool>(type: "boolean", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    scheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    durations = table.Column<int[]>(type: "integer[]", nullable: false),
                    startIncrementMinutes = table.Column<int>(type: "integer", nullable: false),
                    minimumNoticeMinutes = table.Column<int>(type: "integer", nullable: false),
                    dayPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    nightPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    nightStartsAtMinute = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_court", x => x.id);
                    table.ForeignKey(
                        name: "FK_court_schedule_scheduleId",
                        column: x => x.scheduleId,
                        principalSchema: "bookings",
                        principalTable: "schedule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_court_scheduleId",
                schema: "bookings",
                table: "court",
                column: "scheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_court_tenantId",
                schema: "bookings",
                table: "court",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "uxCourtTenantSportSortOrder",
                schema: "bookings",
                table: "court",
                columns: new[] { "tenantId", "sport", "sortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_tenantId",
                schema: "bookings",
                table: "schedule",
                column: "tenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "court",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "schedule",
                schema: "bookings");
        }
    }
}
