using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCropCalendarTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CropCalendarEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BanglaName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ScientificName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Season = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SowingMonths = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GrowingMonths = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HarvestingMonths = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationDays = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OptimalTemperature = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SoilTypes = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    WaterRequirement = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PopularVarieties = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MajorDistricts = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Division = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KeyTips = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IconClass = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BadgeColor = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProfileCropName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropCalendarEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropCalendarEntries_Category",
                table: "CropCalendarEntries",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CropCalendarEntries_ProfileCropName",
                table: "CropCalendarEntries",
                column: "ProfileCropName");

            migrationBuilder.CreateIndex(
                name: "IX_CropCalendarEntries_Season",
                table: "CropCalendarEntries",
                column: "Season");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropCalendarEntries");
        }
    }
}
