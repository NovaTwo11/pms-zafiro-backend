using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PmsZafiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigCashierMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FolioTransaction_Folios_FolioId",
                table: "FolioTransaction");

            migrationBuilder.AlterColumn<Guid>(
                name: "FolioId",
                table: "FolioTransaction",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_FolioTransaction_Folios_FolioId",
                table: "FolioTransaction",
                column: "FolioId",
                principalTable: "Folios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FolioTransaction_Folios_FolioId",
                table: "FolioTransaction");

            migrationBuilder.AlterColumn<Guid>(
                name: "FolioId",
                table: "FolioTransaction",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FolioTransaction_Folios_FolioId",
                table: "FolioTransaction",
                column: "FolioId",
                principalTable: "Folios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
