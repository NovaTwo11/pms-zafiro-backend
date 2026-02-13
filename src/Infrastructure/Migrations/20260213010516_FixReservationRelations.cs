using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PmsZafiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixReservationRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Folios_Reservations_ReservationId",
                table: "Folios");

            migrationBuilder.DropForeignKey(
                name: "FK_FolioTransaction_CashierShifts_CashierShiftId",
                table: "FolioTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_FolioTransaction_Folios_FolioId",
                table: "FolioTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Rooms_RoomId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_RoomId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Folios_ReservationId",
                table: "Folios");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FolioTransaction",
                table: "FolioTransaction");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Alias",
                table: "Folios");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Folios");

            migrationBuilder.DropColumn(
                name: "FolioType",
                table: "Folios");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Folios");

            migrationBuilder.RenameTable(
                name: "FolioTransaction",
                newName: "FolioTransactions");

            migrationBuilder.RenameIndex(
                name: "IX_FolioTransaction_FolioId",
                table: "FolioTransactions",
                newName: "IX_FolioTransactions_FolioId");

            migrationBuilder.RenameIndex(
                name: "IX_FolioTransaction_CashierShiftId",
                table: "FolioTransactions",
                newName: "IX_FolioTransactions_CashierShiftId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FolioTransactions",
                table: "FolioTransactions",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ExternalFolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalFolios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalFolios_Folios_Id",
                        column: x => x.Id,
                        principalTable: "Folios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuestFolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestFolios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestFolios_Folios_Id",
                        column: x => x.Id,
                        principalTable: "Folios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestFolios_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReservationSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckIn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckOut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationSegments_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReservationSegments_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestFolios_ReservationId",
                table: "GuestFolios",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationSegments_ReservationId",
                table: "ReservationSegments",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationSegments_RoomId",
                table: "ReservationSegments",
                column: "RoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_FolioTransactions_CashierShifts_CashierShiftId",
                table: "FolioTransactions",
                column: "CashierShiftId",
                principalTable: "CashierShifts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FolioTransactions_Folios_FolioId",
                table: "FolioTransactions",
                column: "FolioId",
                principalTable: "Folios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FolioTransactions_CashierShifts_CashierShiftId",
                table: "FolioTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FolioTransactions_Folios_FolioId",
                table: "FolioTransactions");

            migrationBuilder.DropTable(
                name: "ExternalFolios");

            migrationBuilder.DropTable(
                name: "GuestFolios");

            migrationBuilder.DropTable(
                name: "ReservationSegments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FolioTransactions",
                table: "FolioTransactions");

            migrationBuilder.RenameTable(
                name: "FolioTransactions",
                newName: "FolioTransaction");

            migrationBuilder.RenameIndex(
                name: "IX_FolioTransactions_FolioId",
                table: "FolioTransaction",
                newName: "IX_FolioTransaction_FolioId");

            migrationBuilder.RenameIndex(
                name: "IX_FolioTransactions_CashierShiftId",
                table: "FolioTransaction",
                newName: "IX_FolioTransaction_CashierShiftId");

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "Folios",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Folios",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolioType",
                table: "Folios",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "Folios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_FolioTransaction",
                table: "FolioTransaction",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RoomId",
                table: "Reservations",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Folios_ReservationId",
                table: "Folios",
                column: "ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Folios_Reservations_ReservationId",
                table: "Folios",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FolioTransaction_CashierShifts_CashierShiftId",
                table: "FolioTransaction",
                column: "CashierShiftId",
                principalTable: "CashierShifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FolioTransaction_Folios_FolioId",
                table: "FolioTransaction",
                column: "FolioId",
                principalTable: "Folios",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Rooms_RoomId",
                table: "Reservations",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
