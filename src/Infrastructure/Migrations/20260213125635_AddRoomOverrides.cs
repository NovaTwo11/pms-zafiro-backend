using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PmsZafiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomPriceOverride_Rooms_RoomId",
                table: "RoomPriceOverride");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RoomPriceOverride",
                table: "RoomPriceOverride");

            migrationBuilder.RenameTable(
                name: "RoomPriceOverride",
                newName: "RoomPriceOverrides");

            migrationBuilder.RenameIndex(
                name: "IX_RoomPriceOverride_RoomId",
                table: "RoomPriceOverrides",
                newName: "IX_RoomPriceOverrides_RoomId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RoomPriceOverrides",
                table: "RoomPriceOverrides",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomPriceOverrides_Rooms_RoomId",
                table: "RoomPriceOverrides",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomPriceOverrides_Rooms_RoomId",
                table: "RoomPriceOverrides");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RoomPriceOverrides",
                table: "RoomPriceOverrides");

            migrationBuilder.RenameTable(
                name: "RoomPriceOverrides",
                newName: "RoomPriceOverride");

            migrationBuilder.RenameIndex(
                name: "IX_RoomPriceOverrides_RoomId",
                table: "RoomPriceOverride",
                newName: "IX_RoomPriceOverride_RoomId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RoomPriceOverride",
                table: "RoomPriceOverride",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomPriceOverride_Rooms_RoomId",
                table: "RoomPriceOverride",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
