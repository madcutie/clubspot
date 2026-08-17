using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubSpot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "clubs",
                schema: "public",
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
                    table.PrimaryKey("pkClubs", x => x.id);
                    table.CheckConstraint("ckClubsDepositPercent", "\"depositPercent\" BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "people",
                schema: "public",
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
                    isBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<Guid>(type: "uuid", nullable: true),
                    debtAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    debtCurrency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkPeople", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedules",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    weeklyRanges = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkSchedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
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
                    table.PrimaryKey("pkUsers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clubModules",
                schema: "public",
                columns: table => new
                {
                    clubId = table.Column<Guid>(type: "uuid", nullable: false),
                    moduleId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contractedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkClubModules", x => new { x.clubId, x.moduleId });
                    table.ForeignKey(
                        name: "fkClubModulesClubId",
                        column: x => x.clubId,
                        principalSchema: "public",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personNotes",
                schema: "public",
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
                    table.PrimaryKey("pkPersonNotes", x => x.id);
                    table.ForeignKey(
                        name: "fkPersonNotesPersonId",
                        column: x => x.personId,
                        principalSchema: "public",
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "courts",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    sport = table.Column<string>(type: "text", nullable: false),
                    sortOrder = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    isCovered = table.Column<bool>(type: "boolean", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    scheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    durations = table.Column<int[]>(type: "integer[]", nullable: false),
                    startIncrementMinutes = table.Column<int>(type: "integer", nullable: false),
                    minimumNoticeMinutes = table.Column<int>(type: "integer", nullable: false),
                    nightStartsAtMinute = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    dayPriceAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    dayPriceCurrency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    nightPriceAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    nightPriceCurrency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkCourts", x => x.id);
                    table.ForeignKey(
                        name: "fkCourtsScheduleId",
                        column: x => x.scheduleId,
                        principalSchema: "public",
                        principalTable: "schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "userRoles",
                schema: "public",
                columns: table => new
                {
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    userId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkUserRoles", x => new { x.userId, x.role });
                    table.ForeignKey(
                        name: "fkUserRolesUserId",
                        column: x => x.userId,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "availabilityOverrides",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    courtId = table.Column<Guid>(type: "uuid", nullable: true),
                    windows = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkAvailabilityOverrides", x => x.id);
                    table.ForeignKey(
                        name: "fkAvailabilityOverridesCourtId",
                        column: x => x.courtId,
                        principalSchema: "public",
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "availabilityOverrideDates",
                schema: "public",
                columns: table => new
                {
                    overrideId = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    tenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pkAvailabilityOverrideDates", x => new { x.overrideId, x.date });
                    table.ForeignKey(
                        name: "fkAvailabilityOverrideDatesOverrideId",
                        column: x => x.overrideId,
                        principalSchema: "public",
                        principalTable: "availabilityOverrides",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ixAvailabilityOverrideDatesTenantId",
                schema: "public",
                table: "availabilityOverrideDates",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "ixAvailabilityOverrideDatesTenantIdDate",
                schema: "public",
                table: "availabilityOverrideDates",
                columns: new[] { "tenantId", "date" });

            migrationBuilder.CreateIndex(
                name: "ixAvailabilityOverridesCourtId",
                schema: "public",
                table: "availabilityOverrides",
                column: "courtId");

            migrationBuilder.CreateIndex(
                name: "ixAvailabilityOverridesTenantId",
                schema: "public",
                table: "availabilityOverrides",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "ixClubModulesClubId",
                schema: "public",
                table: "clubModules",
                column: "clubId");

            migrationBuilder.CreateIndex(
                name: "uxClubsSlug",
                schema: "public",
                table: "clubs",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ixCourtsScheduleId",
                schema: "public",
                table: "courts",
                column: "scheduleId");

            migrationBuilder.CreateIndex(
                name: "ixCourtsTenantId",
                schema: "public",
                table: "courts",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "uxCourtsTenantIdSportSortOrder",
                schema: "public",
                table: "courts",
                columns: new[] { "tenantId", "sport", "sortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ixPeopleTenantId",
                schema: "public",
                table: "people",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "ixPeopleTenantIdPhoneDigits",
                schema: "public",
                table: "people",
                columns: new[] { "tenantId", "phoneDigits" });

            migrationBuilder.CreateIndex(
                name: "ixPeopleTenantIdSearchName",
                schema: "public",
                table: "people",
                columns: new[] { "tenantId", "searchName" });

            migrationBuilder.CreateIndex(
                name: "ixPersonNotesPersonId",
                schema: "public",
                table: "personNotes",
                column: "personId");

            migrationBuilder.CreateIndex(
                name: "ixPersonNotesTenantId",
                schema: "public",
                table: "personNotes",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "ixPersonNotesTenantIdPersonIdCreatedAt",
                schema: "public",
                table: "personNotes",
                columns: new[] { "tenantId", "personId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "ixSchedulesTenantId",
                schema: "public",
                table: "schedules",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "ixUsersTenantId",
                schema: "public",
                table: "users",
                column: "tenantId");

            migrationBuilder.CreateIndex(
                name: "uxUsersTenantIdEmail",
                schema: "public",
                table: "users",
                columns: new[] { "tenantId", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availabilityOverrideDates",
                schema: "public");

            migrationBuilder.DropTable(
                name: "clubModules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "personNotes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "userRoles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "availabilityOverrides",
                schema: "public");

            migrationBuilder.DropTable(
                name: "clubs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "people",
                schema: "public");

            migrationBuilder.DropTable(
                name: "users",
                schema: "public");

            migrationBuilder.DropTable(
                name: "courts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "schedules",
                schema: "public");
        }
    }
}
