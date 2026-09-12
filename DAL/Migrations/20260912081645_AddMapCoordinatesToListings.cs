using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMapCoordinatesToListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Godowns",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Godowns",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Equipment",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Equipment",
                type: "float",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_Latitude_Longitude",
                table: "Godowns",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Latitude_Longitude",
                table: "Equipment",
                columns: new[] { "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Godowns_Latitude_Longitude",
                table: "Godowns");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_Latitude_Longitude",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Equipment");
        }
    }
}
