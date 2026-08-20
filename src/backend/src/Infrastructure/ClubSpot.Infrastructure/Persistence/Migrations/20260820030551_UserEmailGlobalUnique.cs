using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserEmailGlobalUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uxUsersTenantIdEmail",
                schema: "public",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "uxUsersEmail",
                schema: "public",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uxUsersEmail",
                schema: "public",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "uxUsersTenantIdEmail",
                schema: "public",
                table: "users",
                columns: new[] { "tenantId", "email" },
                unique: true);
        }
    }
}
