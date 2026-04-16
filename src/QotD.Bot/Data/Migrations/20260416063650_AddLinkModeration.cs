using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkFilterBypassChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkFilterBypassChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkFilterBypassRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkFilterBypassRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkFilterConfigs",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    LogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SendDirectMessageWarning = table.Column<bool>(type: "boolean", nullable: false),
                    SendChannelWarning = table.Column<bool>(type: "boolean", nullable: false),
                    DirectMessageTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChannelWarningTemplate = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkFilterConfigs", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "LinkFilterRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    NormalizedDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkFilterRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LinkFilterBypassChannels_GuildId",
                table: "LinkFilterBypassChannels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkFilterBypassChannels_GuildId_ChannelId",
                table: "LinkFilterBypassChannels",
                columns: new[] { "GuildId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkFilterBypassRoles_GuildId",
                table: "LinkFilterBypassRoles",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkFilterBypassRoles_GuildId_RoleId",
                table: "LinkFilterBypassRoles",
                columns: new[] { "GuildId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkFilterConfigs_LogChannelId",
                table: "LinkFilterConfigs",
                column: "LogChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkFilterRules_GuildId",
                table: "LinkFilterRules",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkFilterRules_GuildId_NormalizedDomain",
                table: "LinkFilterRules",
                columns: new[] { "GuildId", "NormalizedDomain" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkFilterBypassChannels");

            migrationBuilder.DropTable(
                name: "LinkFilterBypassRoles");

            migrationBuilder.DropTable(
                name: "LinkFilterConfigs");

            migrationBuilder.DropTable(
                name: "LinkFilterRules");
        }
    }
}
