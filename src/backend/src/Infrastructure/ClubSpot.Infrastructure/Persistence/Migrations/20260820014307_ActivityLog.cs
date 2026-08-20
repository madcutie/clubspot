using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activityLogEntries",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    occurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    actorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    actorName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    bookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    personId = table.Column<Guid>(type: "uuid", nullable: true),
                    paymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkActivityLogEntries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ixActivityLogEntriesTenantId",
                schema: "public",
                table: "activityLogEntries",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "ixActivityLogEntriesTenantIdBookingId",
                schema: "public",
                table: "activityLogEntries",
                columns: new[] { "tenantId", "bookingId" });

            migrationBuilder.CreateIndex(
                name: "ixActivityLogEntriesTenantIdOccurredAt",
                schema: "public",
                table: "activityLogEntries",
                columns: new[] { "tenantId", "occurredAt" });

            migrationBuilder.CreateIndex(
                name: "ixActivityLogEntriesTenantIdPersonId",
                schema: "public",
                table: "activityLogEntries",
                columns: new[] { "tenantId", "personId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activityLogEntries",
                schema: "public");
        }
    }
}
