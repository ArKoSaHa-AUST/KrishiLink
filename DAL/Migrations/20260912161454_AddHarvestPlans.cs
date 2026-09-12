using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddHarvestPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HarvestPlanId",
                table: "GodownBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HarvestPlanId",
                table: "EquipmentBookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HarvestPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FarmerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Crop = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarvestPlans_AspNetUsers_FarmerId",
                        column: x => x.FarmerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HarvestPlanItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HarvestPlanId = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Units = table.Column<int>(type: "int", nullable: false),
                    Tons = table.Column<double>(type: "float", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarvestPlanItems_HarvestPlans_HarvestPlanId",
                        column: x => x.HarvestPlanId,
                        principalTable: "HarvestPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_HarvestPlanId",
                table: "GodownBookings",
                column: "HarvestPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_HarvestPlanId",
                table: "EquipmentBookings",
                column: "HarvestPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_HarvestPlanItems_HarvestPlanId",
                table: "HarvestPlanItems",
                column: "HarvestPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_HarvestPlans_FarmerId_Status",
                table: "HarvestPlans",
                columns: new[] { "FarmerId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentBookings_HarvestPlans_HarvestPlanId",
                table: "EquipmentBookings",
                column: "HarvestPlanId",
                principalTable: "HarvestPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GodownBookings_HarvestPlans_HarvestPlanId",
                table: "GodownBookings",
                column: "HarvestPlanId",
                principalTable: "HarvestPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentBookings_HarvestPlans_HarvestPlanId",
                table: "EquipmentBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_GodownBookings_HarvestPlans_HarvestPlanId",
                table: "GodownBookings");

            migrationBuilder.DropTable(
                name: "HarvestPlanItems");

            migrationBuilder.DropTable(
                name: "HarvestPlans");

            migrationBuilder.DropIndex(
                name: "IX_GodownBookings_HarvestPlanId",
                table: "GodownBookings");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentBookings_HarvestPlanId",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "HarvestPlanId",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "HarvestPlanId",
                table: "EquipmentBookings");
        }
    }
}
