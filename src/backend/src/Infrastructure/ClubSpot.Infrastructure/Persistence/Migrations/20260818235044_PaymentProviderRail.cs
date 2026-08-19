using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentProviderRail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "gateway",
                schema: "public",
                table: "payments",
                newName: "provider");

            migrationBuilder.RenameIndex(
                name: "uxPaymentsGatewayExternalId",
                schema: "public",
                table: "payments",
                newName: "uxPaymentsProviderExternalId");

            migrationBuilder.AddColumn<string>(
                name: "rail",
                schema: "public",
                table: "payments",
                type: "text",
                nullable: false,
                // Every pre-existing payment was settled through the hosted checkout.
                defaultValue: "Checkout");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rail",
                schema: "public",
                table: "payments");

            migrationBuilder.RenameColumn(
                name: "provider",
                schema: "public",
                table: "payments",
                newName: "gateway");

            migrationBuilder.RenameIndex(
                name: "uxPaymentsProviderExternalId",
                schema: "public",
                table: "payments",
                newName: "uxPaymentsGatewayExternalId");
        }
    }
}
