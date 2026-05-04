using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ilkimPlastik.WEB.Migrations
{
    /// <inheritdoc />
    public partial class cargoinfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CargoCompany",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CargoTrackingNumber",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CargoCompany",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CargoTrackingNumber",
                table: "Orders");
        }
    }
}
