using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingVerifyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerifyToken",
                schema: "krishilink",
                table: "GodownBookings",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifyToken",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerifyToken",
                schema: "krishilink",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "VerifyToken",
                schema: "krishilink",
                table: "EquipmentBookings");
        }
    }
}
