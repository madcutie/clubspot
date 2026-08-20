using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ixPeopleTenantIdEmail",
                schema: "public",
                table: "people",
                columns: new[] { "tenantId", "email" });

            // The tenant joins the exclusion key. Two clubs never shared a courtId, so nothing was
            // reachable through this, but the invariant was resting on how ids happen to be generated
            // instead of being written down.
            migrationBuilder.Sql("""
                ALTER TABLE public.bookings DROP CONSTRAINT "exBookingsCourtIdDateSlot";
                ALTER TABLE public.bookings ADD CONSTRAINT "exBookingsTenantIdCourtIdDateSlot"
                EXCLUDE USING gist ("tenantId" WITH =, "courtId" WITH =, date WITH =,
                    int4range("startMinute", "startMinute" + "durationMinutes") WITH &&)
                WHERE (status IN ('Confirmed', 'PendingPayment'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.bookings DROP CONSTRAINT "exBookingsTenantIdCourtIdDateSlot";
                ALTER TABLE public.bookings ADD CONSTRAINT "exBookingsCourtIdDateSlot"
                EXCLUDE USING gist ("courtId" WITH =, date WITH =,
                    int4range("startMinute", "startMinute" + "durationMinutes") WITH &&)
                WHERE (status IN ('Confirmed', 'PendingPayment'));
                """);

            migrationBuilder.DropIndex(
                name: "ixPeopleTenantIdEmail",
                schema: "public",
                table: "people");
        }
    }
}
