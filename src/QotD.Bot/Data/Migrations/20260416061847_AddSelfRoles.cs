using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SelfRoleConfigs",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PanelChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    PanelMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ModerationChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AllowMultipleRoles = table.Column<bool>(type: "boolean", nullable: false),
                    RequireModeration = table.Column<bool>(type: "boolean", nullable: false),
                    PanelTitle = table.Column<string>(type: "text", nullable: true),
                    PanelDescriptionTemplate = table.Column<string>(type: "text", nullable: true),
                    PanelFooter = table.Column<string>(type: "text", nullable: true),
                    PanelColorHex = table.Column<string>(type: "text", nullable: true),
                    PanelThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    PanelImageUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfRoleConfigs", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "SelfRoleGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsExclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfRoleGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelfRoleRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PanelMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ModerationChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ModerationMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfRoleRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelfRoleOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EmojiKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfRoleOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SelfRoleOptions_SelfRoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SelfRoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleConfigs_ModerationChannelId",
                table: "SelfRoleConfigs",
                column: "ModerationChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleConfigs_PanelChannelId",
                table: "SelfRoleConfigs",
                column: "PanelChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleGroups_GuildId_DisplayOrder",
                table: "SelfRoleGroups",
                columns: new[] { "GuildId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleGroups_GuildId_Name",
                table: "SelfRoleGroups",
                columns: new[] { "GuildId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleOptions_GroupId",
                table: "SelfRoleOptions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleOptions_GuildId_DisplayOrder",
                table: "SelfRoleOptions",
                columns: new[] { "GuildId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleOptions_GuildId_EmojiKey",
                table: "SelfRoleOptions",
                columns: new[] { "GuildId", "EmojiKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleOptions_GuildId_RoleId",
                table: "SelfRoleOptions",
                columns: new[] { "GuildId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleRequests_GuildId_Status_RequestedAtUtc",
                table: "SelfRoleRequests",
                columns: new[] { "GuildId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfRoleRequests_GuildId_UserId_RoleId_Status",
                table: "SelfRoleRequests",
                columns: new[] { "GuildId", "UserId", "RoleId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SelfRoleConfigs");

            migrationBuilder.DropTable(
                name: "SelfRoleOptions");

            migrationBuilder.DropTable(
                name: "SelfRoleRequests");

            migrationBuilder.DropTable(
                name: "SelfRoleGroups");
        }
    }
}
