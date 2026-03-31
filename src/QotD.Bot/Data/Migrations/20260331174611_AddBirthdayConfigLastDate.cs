using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthdayConfigLastDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastAnnouncementDate",
                table: "BirthdayConfigs",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAnnouncementDate",
                table: "BirthdayConfigs");
        }
    }
}
