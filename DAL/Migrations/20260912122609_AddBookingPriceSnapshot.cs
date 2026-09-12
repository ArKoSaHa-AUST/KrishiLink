using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPriceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AgreedGross",
                table: "GodownBookings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedRate",
                table: "GodownBookings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "GodownBookings",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedOn",
                table: "GodownBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedGross",
                table: "EquipmentBookings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedRate",
                table: "EquipmentBookings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "EquipmentBookings",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedOn",
                table: "EquipmentBookings",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreedGross",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "AgreedRate",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "CompletedOn",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "AgreedGross",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "AgreedRate",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "CompletedOn",
                table: "EquipmentBookings");
        }
    }
}
