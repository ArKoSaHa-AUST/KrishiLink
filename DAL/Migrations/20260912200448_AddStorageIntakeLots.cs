using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageIntakeLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageIntakeLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GodownBookingId = table.Column<int>(type: "int", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IntakeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Crop = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Variety = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Bags = table.Column<int>(type: "int", nullable: false),
                    BagWeightKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    NetWeightKg = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MoisturePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Ungraded"),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Stored"),
                    ReleasedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedTo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ReleaseRemarks = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageIntakeLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageIntakeLots_GodownBookings_GodownBookingId",
                        column: x => x.GodownBookingId,
                        principalTable: "GodownBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageIntakeLots_GodownBookingId",
                table: "StorageIntakeLots",
                column: "GodownBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageIntakeLots_ReceiptNumber",
                table: "StorageIntakeLots",
                column: "ReceiptNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorageIntakeLots");
        }
    }
}
