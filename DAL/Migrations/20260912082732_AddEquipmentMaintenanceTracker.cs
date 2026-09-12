using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentMaintenanceTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EquipmentMaintenanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ServicedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentMaintenanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentMaintenanceRecords_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentMaintenanceRecords_EquipmentId",
                table: "EquipmentMaintenanceRecords",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentMaintenanceRecords_EquipmentId_ServiceDate",
                table: "EquipmentMaintenanceRecords",
                columns: new[] { "EquipmentId", "ServiceDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentMaintenanceRecords");
        }
    }
}
