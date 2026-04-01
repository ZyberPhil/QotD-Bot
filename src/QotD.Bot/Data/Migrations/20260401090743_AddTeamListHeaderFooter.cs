using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamListHeaderFooter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFooter",
                table: "TeamListConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomTitle",
                table: "TeamListConfigs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomFooter",
                table: "TeamListConfigs");

            migrationBuilder.DropColumn(
                name: "CustomTitle",
                table: "TeamListConfigs");
        }
    }
}
