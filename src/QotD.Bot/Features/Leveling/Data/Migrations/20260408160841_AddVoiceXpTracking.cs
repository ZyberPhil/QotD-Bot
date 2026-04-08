using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QotD.Bot.Features.Leveling.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceXpTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastVoiceXpAtUtc",
                table: "LevelUserStats",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastVoiceXpAtUtc",
                table: "LevelUserStats");
        }
    }
}
