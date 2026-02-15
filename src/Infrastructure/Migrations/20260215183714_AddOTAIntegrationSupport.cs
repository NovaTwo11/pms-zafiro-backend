using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PmsZafiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOTAIntegrationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "Reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ChannelCommission",
                table: "Reservations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReservationId",
                table: "Reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalStatus",
                table: "Reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AliasEmail",
                table: "Guests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChannelRoomMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    RoomCategory = table.Column<string>(type: "text", nullable: false),
                    ExternalRoomId = table.Column<string>(type: "text", nullable: false),
                    ExternalRatePlanId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelRoomMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelRoomMappings_Channel_ExternalRoomId_ExternalRatePlan~",
                table: "ChannelRoomMappings",
                columns: new[] { "Channel", "ExternalRoomId", "ExternalRatePlanId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelRoomMappings");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ChannelCommission",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ExternalReservationId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ExternalStatus",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "AliasEmail",
                table: "Guests");
        }
    }
}
