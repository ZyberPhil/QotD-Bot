using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMiniGamesChannelIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CountingChannels_ChannelId",
                table: "CountingChannels",
                column: "ChannelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WordChainConfigs_ChannelId",
                table: "WordChainConfigs",
                column: "ChannelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CountingChannels_ChannelId",
                table: "CountingChannels");

            migrationBuilder.DropIndex(
                name: "IX_WordChainConfigs_ChannelId",
                table: "WordChainConfigs");
        }
    }
}
