using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoModerationAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    RuleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1800)", maxLength: 1800, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoModerationAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoModerationConfigs",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RaidModeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RaidJoinThreshold = table.Column<int>(type: "integer", nullable: false),
                    RaidWindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    RaidLockdownMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsLockdownActive = table.Column<bool>(type: "boolean", nullable: false),
                    LockdownActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockdownEndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RestrictToVerifiedRoleDuringLockdown = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LockdownMinAccountAgeHours = table.Column<int>(type: "integer", nullable: false),
                    EnforceAccountAgeForLinks = table.Column<bool>(type: "boolean", nullable: false),
                    MinAccountAgeDaysForLinks = table.Column<int>(type: "integer", nullable: false),
                    EnforceServerAgeForLinks = table.Column<bool>(type: "boolean", nullable: false),
                    MinServerAgeHoursForLinks = table.Column<int>(type: "integer", nullable: false),
                    LogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoModerationConfigs", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "AutoModerationRaidIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TriggerJoinCount = table.Column<int>(type: "integer", nullable: false),
                    WindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoModerationRaidIncidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationAuditEntries_GuildId",
                table: "AutoModerationAuditEntries",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationAuditEntries_GuildId_CreatedAtUtc",
                table: "AutoModerationAuditEntries",
                columns: new[] { "GuildId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationAuditEntries_GuildId_RuleKey_CreatedAtUtc",
                table: "AutoModerationAuditEntries",
                columns: new[] { "GuildId", "RuleKey", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationAuditEntries_GuildId_UserId_CreatedAtUtc",
                table: "AutoModerationAuditEntries",
                columns: new[] { "GuildId", "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationConfigs_IsEnabled",
                table: "AutoModerationConfigs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationConfigs_LogChannelId",
                table: "AutoModerationConfigs",
                column: "LogChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationRaidIncidents_GuildId",
                table: "AutoModerationRaidIncidents",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationRaidIncidents_GuildId_EndedAtUtc",
                table: "AutoModerationRaidIncidents",
                columns: new[] { "GuildId", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoModerationRaidIncidents_GuildId_StartedAtUtc",
                table: "AutoModerationRaidIncidents",
                columns: new[] { "GuildId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoModerationAuditEntries");

            migrationBuilder.DropTable(
                name: "AutoModerationConfigs");

            migrationBuilder.DropTable(
                name: "AutoModerationRaidIncidents");
        }
    }
}
