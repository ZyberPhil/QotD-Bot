using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QotD.Bot.Features.Leveling.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceXpGuildSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VoiceAllowSelfMutedOrDeafened",
                table: "LevelingConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VoiceMinActiveUsers",
                table: "LevelingConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoiceAllowSelfMutedOrDeafened",
                table: "LevelingConfigs");

            migrationBuilder.DropColumn(
                name: "VoiceMinActiveUsers",
                table: "LevelingConfigs");
        }
    }
}
