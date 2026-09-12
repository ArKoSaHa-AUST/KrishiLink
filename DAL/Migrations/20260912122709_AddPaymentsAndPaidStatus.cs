using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsAndPaidStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidOn",
                table: "GodownBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentId",
                table: "GodownBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidOn",
                table: "EquipmentBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentId",
                table: "EquipmentBookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    FarmerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PayerAccount = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GatewayReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefundedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_FarmerId",
                        column: x => x.FarmerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_PaymentId",
                table: "GodownBookings",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_PaymentId",
                table: "EquipmentBookings",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingType_BookingId",
                table: "Payments",
                columns: new[] { "BookingType", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_FarmerId",
                table: "Payments",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayReference",
                table: "Payments",
                column: "GatewayReference");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Reference",
                table: "Payments",
                column: "Reference",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentBookings_Payments_PaymentId",
                table: "EquipmentBookings",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GodownBookings_Payments_PaymentId",
                table: "GodownBookings",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentBookings_Payments_PaymentId",
                table: "EquipmentBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_GodownBookings_Payments_PaymentId",
                table: "GodownBookings");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_GodownBookings_PaymentId",
                table: "GodownBookings");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentBookings_PaymentId",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "PaidOn",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "PaidOn",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "EquipmentBookings");
        }
    }
}
