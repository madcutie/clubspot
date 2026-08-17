using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Bookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    courtId = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    startMinute = table.Column<int>(type: "integer", nullable: false),
                    durationMinutes = table.Column<int>(type: "integer", nullable: false),
                    customerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    customerPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    priceAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    priceCurrency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkBookings", x => x.id);
                    table.ForeignKey(
                        name: "fkBookingsCourtId",
                        column: x => x.courtId,
                        principalSchema: "public",
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ixBookingsCourtIdDate",
                schema: "public",
                table: "bookings",
                columns: new[] { "courtId", "date" });

            migrationBuilder.CreateIndex(
                name: "ixBookingsTenantId",
                schema: "public",
                table: "bookings",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "ixBookingsTenantIdDate",
                schema: "public",
                table: "bookings",
                columns: new[] { "tenantId", "date" });

            migrationBuilder.Sql("""
                ALTER TABLE public.bookings ADD CONSTRAINT "exBookingsCourtIdDateSlot"
                EXCLUDE USING gist ("courtId" WITH =, date WITH =,
                    int4range("startMinute", "startMinute" + "durationMinutes") WITH &&)
                WHERE (status = 'Confirmed');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.bookings DROP CONSTRAINT "exBookingsCourtIdDateSlot";
                """);

            migrationBuilder.DropTable(
                name: "bookings",
                schema: "public");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
