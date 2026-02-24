using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ilkimPlastik.WEB.Migrations
{
    /// <inheritdoc />
    public partial class addedfaviconfilename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaviconFileName",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaviconFileName",
                table: "SiteSettings");
        }
    }
}
