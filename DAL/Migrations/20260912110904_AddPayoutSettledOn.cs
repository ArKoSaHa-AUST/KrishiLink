using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutSettledOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ListingType",
                table: "Transactions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledOn",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Status_TransactionDate",
                table: "Transactions",
                columns: new[] { "Status", "TransactionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Status_TransactionDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ListingType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SettledOn",
                table: "Transactions");
        }
    }
}
