using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DepositPercentHalfOrFull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ckClubsDepositPercent",
                schema: "public",
                table: "clubs");

            migrationBuilder.AddCheckConstraint(
                name: "ckClubsDepositPercent",
                schema: "public",
                table: "clubs",
                sql: "\"depositPercent\" IN (50, 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ckClubsDepositPercent",
                schema: "public",
                table: "clubs");

            migrationBuilder.AddCheckConstraint(
                name: "ckClubsDepositPercent",
                schema: "public",
                table: "clubs",
                sql: "\"depositPercent\" BETWEEN 0 AND 100");
        }
    }
}
