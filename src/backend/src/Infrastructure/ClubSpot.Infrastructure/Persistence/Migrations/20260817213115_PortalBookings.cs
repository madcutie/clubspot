using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortalBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "createdBy",
                schema: "public",
                table: "bookings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "origin",
                schema: "public",
                table: "bookings",
                type: "text",
                nullable: false,
                // Every pre-existing booking was sold at the counter.
                defaultValue: "Counter");

            migrationBuilder.AddColumn<Guid>(
                name: "personId",
                schema: "public",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ixBookingsPersonId",
                schema: "public",
                table: "bookings",
                column: "personId");

            migrationBuilder.AddForeignKey(
                name: "fkBookingsPersonId",
                schema: "public",
                table: "bookings",
                column: "personId",
                principalSchema: "public",
                principalTable: "people",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fkBookingsPersonId",
                schema: "public",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ixBookingsPersonId",
                schema: "public",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "origin",
                schema: "public",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "personId",
                schema: "public",
                table: "bookings");

            migrationBuilder.AlterColumn<Guid>(
                name: "createdBy",
                schema: "public",
                table: "bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
