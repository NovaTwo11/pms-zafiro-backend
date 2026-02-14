using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PmsZafiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuestUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CityOfOrigin",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CityOfOrigin",
                table: "Guests");
        }
    }
}
