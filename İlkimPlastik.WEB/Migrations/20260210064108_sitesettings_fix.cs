using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ilkimPlastik.WEB.Migrations
{
    /// <inheritdoc />
    public partial class sitesettings_fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SiteName",
                table: "SiteSettings");

            migrationBuilder.RenameColumn(
                name: "SeoTitle",
                table: "SiteSettings",
                newName: "WorkingHours");

            migrationBuilder.RenameColumn(
                name: "SeoKeywords",
                table: "SiteSettings",
                newName: "TwitterUrl");

            migrationBuilder.RenameColumn(
                name: "SeoDescription",
                table: "SiteSettings",
                newName: "Title");

            migrationBuilder.AddColumn<string>(
                name: "ApiUrl",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallBackUrl",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IyzicoApiKey",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IyzicoSecretKey",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationEmail",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Author",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "CallBackUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "IyzicoApiKey",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "IyzicoSecretKey",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "NotificationEmail",
                table: "SiteSettings");

            migrationBuilder.RenameColumn(
                name: "WorkingHours",
                table: "SiteSettings",
                newName: "SeoTitle");

            migrationBuilder.RenameColumn(
                name: "TwitterUrl",
                table: "SiteSettings",
                newName: "SeoKeywords");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "SiteSettings",
                newName: "SeoDescription");

            migrationBuilder.AddColumn<string>(
                name: "SiteName",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
