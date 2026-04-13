using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QotD.Bot.Features.Leveling.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLevelActivityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VoiceMinutes",
                table: "LevelUserStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LevelActivityLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LevelActivityLogs_GuildId_OccurredAtUtc_ActivityType",
                table: "LevelActivityLogs",
                columns: new[] { "GuildId", "OccurredAtUtc", "ActivityType" });

            migrationBuilder.CreateIndex(
                name: "IX_LevelActivityLogs_GuildId_UserId_OccurredAtUtc",
                table: "LevelActivityLogs",
                columns: new[] { "GuildId", "UserId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LevelActivityLogs");

            migrationBuilder.DropColumn(
                name: "VoiceMinutes",
                table: "LevelUserStats");
        }
    }
}
