using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QotD.Bot.Features.Leveling.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialLevelingPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LevelingConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    LevelUpChannelId = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelingConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LevelUserStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    XP = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MessageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastMessageXpAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelUserStats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LevelingConfigs_GuildId",
                table: "LevelingConfigs",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LevelUserStats_GuildId_Level_XP",
                table: "LevelUserStats",
                columns: new[] { "GuildId", "Level", "XP" });

            migrationBuilder.CreateIndex(
                name: "IX_LevelUserStats_GuildId_UserId",
                table: "LevelUserStats",
                columns: new[] { "GuildId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LevelingConfigs");

            migrationBuilder.DropTable(
                name: "LevelUserStats");
        }
    }
}
