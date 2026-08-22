using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingDepositPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "depositPercent",
                schema: "public",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ckBookingsDepositPercent",
                schema: "public",
                table: "bookings",
                sql: "\"depositPercent\" IS NULL OR \"depositPercent\" IN (50, 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ckBookingsDepositPercent",
                schema: "public",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "depositPercent",
                schema: "public",
                table: "bookings");
        }
    }
}
