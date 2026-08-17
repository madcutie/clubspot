using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnlinePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expiresAt",
                schema: "public",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paymentMode",
                schema: "public",
                table: "bookings",
                type: "text",
                nullable: false,
                // Every pre-existing booking was paid (or payable) at the club.
                defaultValue: "Club");

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    bookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    externalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkPayments", x => x.id);
                    table.ForeignKey(
                        name: "fkPaymentsBookingId",
                        column: x => x.bookingId,
                        principalSchema: "public",
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ixPaymentsBookingId",
                schema: "public",
                table: "payments",
                column: "bookingId");

            migrationBuilder.CreateIndex(
                name: "ixPaymentsTenantId",
                schema: "public",
                table: "payments",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "uxPaymentsGatewayExternalId",
                schema: "public",
                table: "payments",
                columns: new[] { "gateway", "externalId" },
                unique: true);

            // A live hold blocks the slot exactly like a confirmed booking.
            migrationBuilder.Sql("""
                ALTER TABLE public.bookings DROP CONSTRAINT "exBookingsCourtIdDateSlot";
                ALTER TABLE public.bookings ADD CONSTRAINT "exBookingsCourtIdDateSlot"
                EXCLUDE USING gist ("courtId" WITH =, date WITH =,
                    int4range("startMinute", "startMinute" + "durationMinutes") WITH &&)
                WHERE (status IN ('Confirmed', 'PendingPayment'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.bookings DROP CONSTRAINT "exBookingsCourtIdDateSlot";
                ALTER TABLE public.bookings ADD CONSTRAINT "exBookingsCourtIdDateSlot"
                EXCLUDE USING gist ("courtId" WITH =, date WITH =,
                    int4range("startMinute", "startMinute" + "durationMinutes") WITH &&)
                WHERE (status = 'Confirmed');
                """);

            migrationBuilder.DropTable(
                name: "payments",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "expiresAt",
                schema: "public",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "paymentMode",
                schema: "public",
                table: "bookings");
        }
    }
}
