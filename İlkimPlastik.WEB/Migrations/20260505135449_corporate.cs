using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ilkimPlastik.WEB.Migrations
{
    /// <inheritdoc />
    public partial class corporate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorporate",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxOfficeName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsCorporate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TaxOfficeName",
                table: "Users");
        }
    }
}
