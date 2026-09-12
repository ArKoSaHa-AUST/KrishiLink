using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingsAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "Godowns",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "Godowns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "Equipment",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "Equipment",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FarmerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BookingType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: true),
                    GodownId = table.Column<int>(type: "int", nullable: true),
                    EquipmentBookingId = table.Column<int>(type: "int", nullable: true),
                    GodownBookingId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_AspNetUsers_FarmerId",
                        column: x => x.FarmerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_EquipmentBookings_EquipmentBookingId",
                        column: x => x.EquipmentBookingId,
                        principalTable: "EquipmentBookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_GodownBookings_GodownBookingId",
                        column: x => x.GodownBookingId,
                        principalTable: "GodownBookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Godowns_GodownId",
                        column: x => x.GodownId,
                        principalTable: "Godowns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_EquipmentBookingId",
                table: "Reviews",
                column: "EquipmentBookingId",
                unique: true,
                filter: "[EquipmentBookingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_EquipmentId_CreatedAt",
                table: "Reviews",
                columns: new[] { "EquipmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_FarmerId",
                table: "Reviews",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_GodownBookingId",
                table: "Reviews",
                column: "GodownBookingId",
                unique: true,
                filter: "[GodownBookingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_GodownId_CreatedAt",
                table: "Reviews",
                columns: new[] { "GodownId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "Equipment");
        }
    }
}
