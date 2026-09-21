using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureEnhancementsPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType",
                schema: "krishilink",
                table: "BookingExpenses");

            migrationBuilder.AddColumn<int>(
                name: "HarvestPlanId",
                schema: "krishilink",
                table: "GodownBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModificationCount",
                schema: "krishilink",
                table: "GodownBookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                schema: "krishilink",
                table: "GodownBookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousDetails",
                schema: "krishilink",
                table: "GodownBookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "krishilink",
                table: "GodownBlockedDates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HarvestPlanId",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModificationCount",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedOn",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousDetails",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingNote",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedGross",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Units",
                schema: "krishilink",
                table: "EquipmentBookings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "krishilink",
                table: "EquipmentBlockedDates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinRentalDays",
                schema: "krishilink",
                table: "Equipment",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "krishilink",
                table: "Equipment",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                schema: "krishilink",
                table: "BookingExpenses",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "krishilink",
                table: "BookingExpenses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpenseDate",
                schema: "krishilink",
                table: "BookingExpenses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "ListingId",
                schema: "krishilink",
                table: "BookingExpenses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentRateRules",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: true),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentRateRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentRateRules_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalSchema: "krishilink",
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ListingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ListingId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Favorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HarvestPlans",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmerId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Crop = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarvestPlans_AspNetUsers_FarmerId",
                        column: x => x.FarmerId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedSearches",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ListingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SearchTerm = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    District = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    MaxRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinCapacityTons = table.Column<double>(type: "double precision", nullable: true),
                    From = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    To = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AlertsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAlertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KnownListingIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedSearches_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorageIntakeLots",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GodownBookingId = table.Column<int>(type: "integer", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IntakeDate = table.Column<DateTime>(type: "date", nullable: false),
                    Crop = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Variety = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Bags = table.Column<int>(type: "integer", nullable: false),
                    BagWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    NetWeightKg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MoisturePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Grade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Ungraded"),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Stored"),
                    ReleasedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedTo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReleaseRemarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageIntakeLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageIntakeLots_GodownBookings_GodownBookingId",
                        column: x => x.GodownBookingId,
                        principalSchema: "krishilink",
                        principalTable: "GodownBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HarvestPlanItems",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HarvestPlanId = table.Column<int>(type: "integer", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ListingId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Units = table.Column<int>(type: "integer", nullable: false),
                    Tons = table.Column<double>(type: "double precision", nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarvestPlanItems_HarvestPlans_HarvestPlanId",
                        column: x => x.HarvestPlanId,
                        principalSchema: "krishilink",
                        principalTable: "HarvestPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_HarvestPlanId",
                schema: "krishilink",
                table: "GodownBookings",
                column: "HarvestPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_HarvestPlanId",
                schema: "krishilink",
                table: "EquipmentBookings",
                column: "HarvestPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType_ExpenseDate",
                schema: "krishilink",
                table: "BookingExpenses",
                columns: new[] { "OwnerId", "BookingType", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRateRules_EquipmentId_IsActive",
                schema: "krishilink",
                table: "EquipmentRateRules",
                columns: new[] { "EquipmentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId_ListingType_ListingId",
                schema: "krishilink",
                table: "Favorites",
                columns: new[] { "UserId", "ListingType", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HarvestPlanItems_HarvestPlanId",
                schema: "krishilink",
                table: "HarvestPlanItems",
                column: "HarvestPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_HarvestPlans_FarmerId_Status",
                schema: "krishilink",
                table: "HarvestPlans",
                columns: new[] { "FarmerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_AlertsEnabled",
                schema: "krishilink",
                table: "SavedSearches",
                column: "AlertsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_UserId",
                schema: "krishilink",
                table: "SavedSearches",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageIntakeLots_GodownBookingId",
                schema: "krishilink",
                table: "StorageIntakeLots",
                column: "GodownBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageIntakeLots_ReceiptNumber",
                schema: "krishilink",
                table: "StorageIntakeLots",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentBookings_HarvestPlans_HarvestPlanId",
                schema: "krishilink",
                table: "EquipmentBookings",
                column: "HarvestPlanId",
                principalSchema: "krishilink",
                principalTable: "HarvestPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GodownBookings_HarvestPlans_HarvestPlanId",
                schema: "krishilink",
                table: "GodownBookings",
                column: "HarvestPlanId",
                principalSchema: "krishilink",
                principalTable: "HarvestPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentBookings_HarvestPlans_HarvestPlanId",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_GodownBookings_HarvestPlans_HarvestPlanId",
                schema: "krishilink",
                table: "GodownBookings");

            migrationBuilder.DropTable(
                name: "EquipmentRateRules",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "Favorites",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "HarvestPlanItems",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "SavedSearches",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "StorageIntakeLots",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "HarvestPlans",
                schema: "krishilink");

            migrationBuilder.DropIndex(
                name: "IX_GodownBookings_HarvestPlanId",
                schema: "krishilink",
                table: "GodownBookings");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentBookings_HarvestPlanId",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType_ExpenseDate",
                schema: "krishilink",
                table: "BookingExpenses");

            migrationBuilder.DropColumn(
                name: "HarvestPlanId",
                schema: "krishilink",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "ModificationCount",
                schema: "krishilink",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                schema: "krishilink",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "PreviousDetails",
                schema: "krishilink",
                table: "GodownBookings");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "krishilink",
                table: "GodownBlockedDates");

            migrationBuilder.DropColumn(
                name: "HarvestPlanId",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "ModificationCount",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "ModifiedOn",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "PreviousDetails",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "PricingNote",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "QuotedGross",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "Units",
                schema: "krishilink",
                table: "EquipmentBookings");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "krishilink",
                table: "EquipmentBlockedDates");

            migrationBuilder.DropColumn(
                name: "MinRentalDays",
                schema: "krishilink",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "krishilink",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "krishilink",
                table: "BookingExpenses");

            migrationBuilder.DropColumn(
                name: "ExpenseDate",
                schema: "krishilink",
                table: "BookingExpenses");

            migrationBuilder.DropColumn(
                name: "ListingId",
                schema: "krishilink",
                table: "BookingExpenses");

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                schema: "krishilink",
                table: "BookingExpenses",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType",
                schema: "krishilink",
                table: "BookingExpenses",
                columns: new[] { "OwnerId", "BookingType" });
        }
    }
}
