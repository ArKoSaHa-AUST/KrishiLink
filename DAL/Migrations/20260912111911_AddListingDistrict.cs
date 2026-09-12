using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddListingDistrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Godowns",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Equipment",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_District",
                table: "Godowns",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_District",
                table: "Equipment",
                column: "District");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Godowns_District",
                table: "Godowns");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_District",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Equipment");
        }
    }
}
