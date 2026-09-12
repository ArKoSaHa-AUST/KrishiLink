using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseCategoriesAndGeneralExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType",
                table: "BookingExpenses");

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "BookingExpenses",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "BookingExpenses",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpenseDate",
                table: "BookingExpenses",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "ListingId",
                table: "BookingExpenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType_ExpenseDate",
                table: "BookingExpenses",
                columns: new[] { "OwnerId", "BookingType", "ExpenseDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType_ExpenseDate",
                table: "BookingExpenses");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "BookingExpenses");

            migrationBuilder.DropColumn(
                name: "ExpenseDate",
                table: "BookingExpenses");

            migrationBuilder.DropColumn(
                name: "ListingId",
                table: "BookingExpenses");

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "BookingExpenses",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType",
                table: "BookingExpenses",
                columns: new[] { "OwnerId", "BookingType" });
        }
    }
}
