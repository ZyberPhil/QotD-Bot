using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QotD.Bot.Features.Leveling.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialLeveling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LevelUserStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    XP = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Level = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastMessageXpAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelUserStats", x => x.Id);
                });

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
                name: "LevelUserStats");
        }
    }
}
