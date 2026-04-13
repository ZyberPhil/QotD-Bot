using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamWeeklyReportsRoleHistoryAndWarningNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamWarnings_GuildId_UserId_IsActive",
                table: "TeamWarnings");

            migrationBuilder.AddColumn<bool>(
                name: "IsResolved",
                table: "TeamWarnings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "TeamWarnings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAtUtc",
                table: "TeamWarnings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ResolvedByUserId",
                table: "TeamWarnings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamRoleChangeHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OldRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    OldRoleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NewRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    NewRoleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRoleChangeHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamWarningNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WarningId = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    AuthorUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    NoteType = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamWarningNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamWarningNotes_TeamWarnings_WarningId",
                        column: x => x.WarningId,
                        principalTable: "TeamWarnings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamWeeklyReportConfigs",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastReportedWeekStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamWeeklyReportConfigs", x => x.GuildId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamWarnings_GuildId_UserId_IsActive_IsResolved",
                table: "TeamWarnings",
                columns: new[] { "GuildId", "UserId", "IsActive", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamRoleChangeHistories_GuildId_UserId_ChangedAtUtc",
                table: "TeamRoleChangeHistories",
                columns: new[] { "GuildId", "UserId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamWarningNotes_GuildId_WarningId_CreatedAtUtc",
                table: "TeamWarningNotes",
                columns: new[] { "GuildId", "WarningId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamWarningNotes_WarningId",
                table: "TeamWarningNotes",
                column: "WarningId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamWeeklyReportConfigs_ChannelId",
                table: "TeamWeeklyReportConfigs",
                column: "ChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamRoleChangeHistories");

            migrationBuilder.DropTable(
                name: "TeamWarningNotes");

            migrationBuilder.DropTable(
                name: "TeamWeeklyReportConfigs");

            migrationBuilder.DropIndex(
                name: "IX_TeamWarnings_GuildId_UserId_IsActive_IsResolved",
                table: "TeamWarnings");

            migrationBuilder.DropColumn(
                name: "IsResolved",
                table: "TeamWarnings");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "TeamWarnings");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "TeamWarnings");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "TeamWarnings");

            migrationBuilder.CreateIndex(
                name: "IX_TeamWarnings_GuildId_UserId_IsActive",
                table: "TeamWarnings",
                columns: new[] { "GuildId", "UserId", "IsActive" });
        }
    }
}
