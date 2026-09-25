using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasonEconomics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CropCalendarEntryId",
                schema: "krishilink",
                table: "HarvestPlans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedPricePerKg",
                schema: "krishilink",
                table: "HarvestPlans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LandSizeDecimal",
                schema: "krishilink",
                table: "HarvestPlans",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLandUnit",
                schema: "krishilink",
                table: "AspNetUsers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeasonCosts",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HarvestPlanId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncurredOn = table.Column<DateTime>(type: "date", nullable: false),
                    RecordedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonCosts_HarvestPlans_HarvestPlanId",
                        column: x => x.HarvestPlanId,
                        principalSchema: "krishilink",
                        principalTable: "HarvestPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HarvestPlans_CropCalendarEntryId",
                schema: "krishilink",
                table: "HarvestPlans",
                column: "CropCalendarEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonCosts_HarvestPlanId",
                schema: "krishilink",
                table: "SeasonCosts",
                column: "HarvestPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_HarvestPlans_CropCalendarEntries_CropCalendarEntryId",
                schema: "krishilink",
                table: "HarvestPlans",
                column: "CropCalendarEntryId",
                principalSchema: "krishilink",
                principalTable: "CropCalendarEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HarvestPlans_CropCalendarEntries_CropCalendarEntryId",
                schema: "krishilink",
                table: "HarvestPlans");

            migrationBuilder.DropTable(
                name: "SeasonCosts",
                schema: "krishilink");

            migrationBuilder.DropIndex(
                name: "IX_HarvestPlans_CropCalendarEntryId",
                schema: "krishilink",
                table: "HarvestPlans");

            migrationBuilder.DropColumn(
                name: "CropCalendarEntryId",
                schema: "krishilink",
                table: "HarvestPlans");

            migrationBuilder.DropColumn(
                name: "ExpectedPricePerKg",
                schema: "krishilink",
                table: "HarvestPlans");

            migrationBuilder.DropColumn(
                name: "LandSizeDecimal",
                schema: "krishilink",
                table: "HarvestPlans");

            migrationBuilder.DropColumn(
                name: "PreferredLandUnit",
                schema: "krishilink",
                table: "AspNetUsers");
        }
    }
}
