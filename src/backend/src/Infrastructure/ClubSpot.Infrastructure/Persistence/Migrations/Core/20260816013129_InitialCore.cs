using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations.Core
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "club",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    venue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    timeZone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    depositPercent = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club", x => x.id);
                    table.CheckConstraint("ckClubDepositPercent", "\"depositPercent\" BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "person",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    searchName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    phoneDigits = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    preferredSport = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    isBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<Guid>(type: "uuid", nullable: true),
                    debtAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    debtCurrency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    passwordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clubModule",
                schema: "core",
                columns: table => new
                {
                    clubId = table.Column<Guid>(type: "uuid", nullable: false),
                    moduleId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contractedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clubModule", x => new { x.clubId, x.moduleId });
                    table.ForeignKey(
                        name: "FK_clubModule_club_clubId",
                        column: x => x.clubId,
                        principalSchema: "core",
                        principalTable: "club",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personNote",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    personId = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    authorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personNote", x => x.id);
                    table.ForeignKey(
                        name: "FK_personNote_person_personId",
                        column: x => x.personId,
                        principalSchema: "core",
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "userRole",
                schema: "core",
                columns: table => new
                {
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    userId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userRole", x => new { x.userId, x.role });
                    table.ForeignKey(
                        name: "FK_userRole_user_userId",
                        column: x => x.userId,
                        principalSchema: "core",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uxClubSlug",
                schema: "core",
                table: "club",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clubModule_clubId",
                schema: "core",
                table: "clubModule",
                column: "clubId");

            migrationBuilder.CreateIndex(
                name: "IX_person_tenantId",
                schema: "core",
                table: "person",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "ixPersonTenantPhoneDigits",
                schema: "core",
                table: "person",
                columns: new[] { "tenantId", "phoneDigits" });

            migrationBuilder.CreateIndex(
                name: "ixPersonTenantSearchName",
                schema: "core",
                table: "person",
                columns: new[] { "tenantId", "searchName" });

            migrationBuilder.CreateIndex(
                name: "IX_personNote_personId",
                schema: "core",
                table: "personNote",
                column: "personId");

            migrationBuilder.CreateIndex(
                name: "IX_personNote_tenantId",
                schema: "core",
                table: "personNote",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "IX_personNote_tenantId_personId_createdAt",
                schema: "core",
                table: "personNote",
                columns: new[] { "tenantId", "personId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_tenantId",
                schema: "core",
                table: "user",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "uxUserTenantEmail",
                schema: "core",
                table: "user",
                columns: new[] { "tenantId", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clubModule",
                schema: "core");

            migrationBuilder.DropTable(
                name: "personNote",
                schema: "core");

            migrationBuilder.DropTable(
                name: "userRole",
                schema: "core");

            migrationBuilder.DropTable(
                name: "club",
                schema: "core");

            migrationBuilder.DropTable(
                name: "person",
                schema: "core");

            migrationBuilder.DropTable(
                name: "user",
                schema: "core");
        }
    }
}
