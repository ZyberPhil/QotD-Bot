using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamActivityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamActivityPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MinMessagesPerWeek = table.Column<int>(type: "integer", nullable: false),
                    MinVoiceMinutesPerWeek = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamActivityPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamActivityWeeklySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WeekStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Messages = table.Column<int>(type: "integer", nullable: false),
                    VoiceMinutes = table.Column<int>(type: "integer", nullable: false),
                    CombinedScore = table.Column<double>(type: "double precision", nullable: false),
                    MeetsMinimum = table.Column<bool>(type: "boolean", nullable: false),
                    WasExcused = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamActivityWeeklySnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamLeaveEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLeaveEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamWarnings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    WarningType = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    WeekStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamWarnings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamActivityPolicies_GuildId",
                table: "TeamActivityPolicies",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamActivityPolicies_GuildId_RoleId",
                table: "TeamActivityPolicies",
                columns: new[] { "GuildId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamActivityWeeklySnapshots_GuildId_UserId",
                table: "TeamActivityWeeklySnapshots",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamActivityWeeklySnapshots_GuildId_WeekStartUtc",
                table: "TeamActivityWeeklySnapshots",
                columns: new[] { "GuildId", "WeekStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamActivityWeeklySnapshots_GuildId_WeekStartUtc_RoleId_Use~",
                table: "TeamActivityWeeklySnapshots",
                columns: new[] { "GuildId", "WeekStartUtc", "RoleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamLeaveEntries_GuildId_UserId_EndUtc",
                table: "TeamLeaveEntries",
                columns: new[] { "GuildId", "UserId", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamLeaveEntries_GuildId_UserId_StartUtc",
                table: "TeamLeaveEntries",
                columns: new[] { "GuildId", "UserId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamWarnings_GuildId_UserId_IsActive",
                table: "TeamWarnings",
                columns: new[] { "GuildId", "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamWarnings_GuildId_WeekStartUtc_UserId_RoleId_WarningType",
                table: "TeamWarnings",
                columns: new[] { "GuildId", "WeekStartUtc", "UserId", "RoleId", "WarningType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamActivityPolicies");

            migrationBuilder.DropTable(
                name: "TeamActivityWeeklySnapshots");

            migrationBuilder.DropTable(
                name: "TeamLeaveEntries");

            migrationBuilder.DropTable(
                name: "TeamWarnings");
        }
    }
}
