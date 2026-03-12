using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPingRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PingRoleId",
                table: "GuildConfigs",
                type: "numeric(20,0)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PingRoleId",
                table: "GuildConfigs");
        }
    }
}
