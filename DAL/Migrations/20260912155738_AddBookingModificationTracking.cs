using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingModificationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModificationCount",
                table: "GodownBookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                table: "GodownBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousDetails",
                table: "GodownBookings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModificationCount",
                table: "EquipmentBookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                table: "EquipmentBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousDetails",
                table: "EquipmentBookings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModificationCount",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "PreviousDetails",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "ModificationCount",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "PreviousDetails",
                table: "EquipmentBookings");
        }
    }
}
