using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddXminConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "krishilink",
                table: "Transactions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "krishilink",
                table: "Payments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "krishilink",
                table: "HarvestPlans",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "krishilink",
                table: "Godowns",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "krishilink",
                table: "GodownBookings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "krishilink",
                table: "Equipment",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "krishilink",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "krishilink",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "krishilink",
                table: "HarvestPlans");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "krishilink",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "krishilink",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "krishilink",
                table: "Equipment");
        }
    }
}
