using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QotD.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaptureModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketConfigs",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PanelDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MaxOpenTicketsPerUser = table.Column<int>(type: "integer", nullable: false),
                    DefaultSlaMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketConfigs", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "TicketLogConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketLogConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TicketNumber = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatedByUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ClaimedByUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketTranscripts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    TicketChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    GeneratedByUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTranscripts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketStaffRoles",
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
                    table.PrimaryKey("PK_TicketStaffRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketStaffRoles_TicketConfigs_GuildId",
                        column: x => x.GuildId,
                        principalTable: "TicketConfigs",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketConfigs_IsEnabled",
                table: "TicketConfigs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_TicketLogConfigs_GuildId_EventType_IsEnabled",
                table: "TicketLogConfigs",
                columns: new[] { "GuildId", "EventType", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_GuildId_ChannelId",
                table: "Tickets",
                columns: new[] { "GuildId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_GuildId_CreatedByUserId_Status",
                table: "Tickets",
                columns: new[] { "GuildId", "CreatedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_GuildId_Status_CreatedAtUtc",
                table: "Tickets",
                columns: new[] { "GuildId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_GuildId_TicketNumber",
                table: "Tickets",
                columns: new[] { "GuildId", "TicketNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketStaffRoles_GuildId_RoleId",
                table: "TicketStaffRoles",
                columns: new[] { "GuildId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketTranscripts_GuildId_TicketId",
                table: "TicketTranscripts",
                columns: new[] { "GuildId", "TicketId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketLogConfigs");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketStaffRoles");

            migrationBuilder.DropTable(
                name: "TicketTranscripts");

            migrationBuilder.DropTable(
                name: "TicketConfigs");
        }
    }
}
