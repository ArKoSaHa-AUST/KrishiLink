using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutLinkageCancellationAndStatementTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledOn",
                table: "GodownBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutId",
                table: "GodownBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledOn",
                table: "EquipmentBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutId",
                table: "EquipmentBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatementSentMonth",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_PayoutId",
                table: "GodownBookings",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_PayoutId",
                table: "EquipmentBookings",
                column: "PayoutId");

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentBookings_Transactions_PayoutId",
                table: "EquipmentBookings",
                column: "PayoutId",
                principalTable: "Transactions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GodownBookings_Transactions_PayoutId",
                table: "GodownBookings",
                column: "PayoutId",
                principalTable: "Transactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentBookings_Transactions_PayoutId",
                table: "EquipmentBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_GodownBookings_Transactions_PayoutId",
                table: "GodownBookings");

            migrationBuilder.DropIndex(
                name: "IX_GodownBookings_PayoutId",
                table: "GodownBookings");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentBookings_PayoutId",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "CancelledOn",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "PayoutId",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "CancelledOn",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "PayoutId",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "LastStatementSentMonth",
                table: "AspNetUsers");
        }
    }
}
