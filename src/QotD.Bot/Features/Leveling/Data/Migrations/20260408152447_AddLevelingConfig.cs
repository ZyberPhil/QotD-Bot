using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QotD.Bot.Features.Leveling.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLevelingConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LevelingConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    LevelUpChannelId = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelingConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LevelingConfigs_GuildId",
                table: "LevelingConfigs",
                column: "GuildId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LevelingConfigs");
        }
    }
}
