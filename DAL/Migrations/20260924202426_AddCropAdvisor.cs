using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCropAdvisor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropRecommendations",
                schema: "krishilink");

            migrationBuilder.AddColumn<string>(
                name: "Key",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KeyTipsBn",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxPh",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinPh",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoilTypesBn",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "TypicalYieldPerAcreMax",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TypicalYieldPerAcreMin",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaterNeed",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.AddColumn<string>(
                name: "WaterRequirementBn",
                schema: "krishilink",
                table: "CropCalendarEntries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SavedCropAdvisories",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CropCalendarEntryId = table.Column<int>(type: "integer", nullable: false),
                    Season = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SoilType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SoilPh = table.Column<double>(type: "double precision", nullable: true),
                    LandSizeDecimal = table.Column<double>(type: "double precision", nullable: true),
                    HasIrrigation = table.Column<bool>(type: "boolean", nullable: false),
                    District = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MatchScore = table.Column<int>(type: "integer", nullable: false),
                    FactorsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedCropAdvisories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedCropAdvisories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedCropAdvisories_CropCalendarEntries_CropCalendarEntryId",
                        column: x => x.CropCalendarEntryId,
                        principalSchema: "krishilink",
                        principalTable: "CropCalendarEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropCalendarEntries_Key",
                schema: "krishilink",
                table: "CropCalendarEntries",
                column: "Key",
                unique: true,
                filter: "\"Key\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_SavedCropAdvisories_CropCalendarEntryId",
                schema: "krishilink",
                table: "SavedCropAdvisories",
                column: "CropCalendarEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedCropAdvisories_UserId_CreatedAt",
                schema: "krishilink",
                table: "SavedCropAdvisories",
                columns: new[] { "UserId", "CreatedAt" });

            // A farmer's saved advice is private: never reachable through Supabase's browser API roles.
            migrationBuilder.Sql("""
                REVOKE ALL ON krishilink."SavedCropAdvisories" FROM PUBLIC;
                DO $permissions$
                DECLARE api_role text;
                BEGIN
                    FOR api_role IN SELECT rolname FROM pg_roles WHERE rolname IN ('anon', 'authenticated')
                    LOOP
                        EXECUTE format('REVOKE ALL ON krishilink."SavedCropAdvisories" FROM %I', api_role);
                    END LOOP;
                END
                $permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedCropAdvisories",
                schema: "krishilink");

            migrationBuilder.DropIndex(
                name: "IX_CropCalendarEntries_Key",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "Key",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "KeyTipsBn",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "MaxPh",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "MinPh",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "SoilTypesBn",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "TypicalYieldPerAcreMax",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "TypicalYieldPerAcreMin",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "WaterNeed",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.DropColumn(
                name: "WaterRequirementBn",
                schema: "krishilink",
                table: "CropCalendarEntries");

            migrationBuilder.CreateTable(
                name: "CropRecommendations",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Details = table.Column<string>(type: "text", nullable: false),
                    RecommendedCrops = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropRecommendations", x => x.Id);
                });
        }
    }
}
