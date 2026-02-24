using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ilkimPlastik.WEB.Migrations
{
    /// <inheritdoc />
    public partial class adeddpdfs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionsPdfName",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistanceSalesAgreementPdfName",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KvkkPdfName",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyPolicyPdfName",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnPolicyPdfName",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionsPdfName",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "DistanceSalesAgreementPdfName",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "KvkkPdfName",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyPdfName",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ReturnPolicyPdfName",
                table: "SiteSettings");
        }
    }
}
