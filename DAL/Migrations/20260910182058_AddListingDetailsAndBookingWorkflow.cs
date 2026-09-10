using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddListingDetailsAndBookingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentBookings_Equipment_EquipmentId",
                table: "EquipmentBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_GodownBookings_Godowns_GodownId",
                table: "GodownBookings");

            migrationBuilder.DropIndex(
                name: "IX_GodownBookings_GodownId",
                table: "GodownBookings");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentBookings_EquipmentId",
                table: "EquipmentBookings");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Godowns",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Godowns",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Godowns",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Godowns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Facilities",
                table: "Godowns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                table: "Godowns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Godowns",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageType",
                table: "Godowns",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "GodownBookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "GodownBookings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "GodownBookings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedOn",
                table: "GodownBookings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOn",
                table: "GodownBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "EquipmentBookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "EquipmentBookings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "EquipmentBookings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedOn",
                table: "EquipmentBookings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOn",
                table: "EquipmentBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Equipment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Equipment",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Equipment",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Equipment",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "Equipment",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                table: "Equipment",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Equipment",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BookingExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingExpenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentBlockedDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentBlockedDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentBlockedDates_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_GodownId_Status",
                table: "GodownBookings",
                columns: new[] { "GodownId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_EquipmentId_Status",
                table: "EquipmentBookings",
                columns: new[] { "EquipmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType",
                table: "BookingExpenses",
                columns: new[] { "OwnerId", "BookingType" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBlockedDates_EquipmentId_Date",
                table: "EquipmentBlockedDates",
                columns: new[] { "EquipmentId", "Date" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentBookings_Equipment_EquipmentId",
                table: "EquipmentBookings",
                column: "EquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GodownBookings_Godowns_GodownId",
                table: "GodownBookings",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentBookings_Equipment_EquipmentId",
                table: "EquipmentBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_GodownBookings_Godowns_GodownId",
                table: "GodownBookings");

            migrationBuilder.DropTable(
                name: "BookingExpenses");

            migrationBuilder.DropTable(
                name: "EquipmentBlockedDates");

            migrationBuilder.DropIndex(
                name: "IX_GodownBookings_GodownId_Status",
                table: "GodownBookings");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentBookings_EquipmentId_Status",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "Facilities",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "ImageUrls",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "StorageType",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "RequestedOn",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "UpdatedOn",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "RequestedOn",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "UpdatedOn",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "ImageUrls",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Equipment");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Godowns",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Godowns",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "GodownBookings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "EquipmentBookings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Equipment",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Equipment",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_GodownId",
                table: "GodownBookings",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_EquipmentId",
                table: "EquipmentBookings",
                column: "EquipmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentBookings_Equipment_EquipmentId",
                table: "EquipmentBookings",
                column: "EquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GodownBookings_Godowns_GodownId",
                table: "GodownBookings",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
