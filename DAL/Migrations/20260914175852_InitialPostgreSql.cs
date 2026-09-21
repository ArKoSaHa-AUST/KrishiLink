using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "krishilink");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    UserRole = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    BusinessOrFarmName = table.Column<string>(type: "text", nullable: true),
                    District = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Specialization = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    OnboardingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatementSentMonth = table.Column<DateTime>(type: "date", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Unverified"),
                    NidNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NidFrontImagePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NidBackImagePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TradeLicenseImagePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    VerificationSubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationRejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VerificationNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LoyaltyPoints = table.Column<int>(type: "integer", nullable: false),
                    OwnerAverageRating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    OwnerReviewCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingExpenses",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingExpenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CropCalendarEntries",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BanglaName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScientificName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Season = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SowingMonths = table.Column<string>(type: "text", nullable: false),
                    GrowingMonths = table.Column<string>(type: "text", nullable: false),
                    HarvestingMonths = table.Column<string>(type: "text", nullable: false),
                    DurationDays = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OptimalTemperature = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SoilTypes = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WaterRequirement = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PopularVarieties = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MajorDistricts = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Division = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyTips = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IconClass = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BadgeColor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProfileCropName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropCalendarEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CropRecommendations",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    RecommendedCrops = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropRecommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Crops",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Season = table.Column<string>(type: "text", nullable: false),
                    SoilType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DebitAccount = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreditAccount = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BookingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    PayoutId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeatherData",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Location = table.Column<string>(type: "text", nullable: false),
                    District = table.Column<string>(type: "text", nullable: true),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    TemperatureMax = table.Column<double>(type: "double precision", nullable: false),
                    TemperatureMin = table.Column<double>(type: "double precision", nullable: false),
                    HumidityMax = table.Column<double>(type: "double precision", nullable: false),
                    HumidityMin = table.Column<double>(type: "double precision", nullable: false),
                    PrecipitationMm = table.Column<double>(type: "double precision", nullable: false),
                    PrecipitationProbability = table.Column<double>(type: "double precision", nullable: false),
                    WeatherCode = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    ForecastDate = table.Column<DateTime>(type: "date", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "krishilink",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "krishilink",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "krishilink",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    District = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ImageUrls = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AverageRating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipment_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Godowns",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StorageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    District = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CapacityInTons = table.Column<double>(type: "double precision", nullable: false),
                    PricePerTonPerMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Facilities = table.Column<string>(type: "text", nullable: false),
                    ImageUrls = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AverageRating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Godowns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Godowns_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPointTransactions",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BookingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    BookingCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PromoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountSpent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPointTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyPointTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TitleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MessageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ArgsJson = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    FarmerId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayerAccount = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GatewayReference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_FarmerId",
                        column: x => x.FarmerId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ListingType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PayoutAccount = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VerificationRequests",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    NidNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NidLast4 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NidFrontImagePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NidBackImagePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TradeLicenseImagePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByAdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentBlockedDates",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentBlockedDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentBlockedDates_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalSchema: "krishilink",
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentMaintenanceRecords",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "date", nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ServicedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentMaintenanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentMaintenanceRecords_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalSchema: "krishilink",
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GodownBlockedDates",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GodownId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GodownBlockedDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GodownBlockedDates_Godowns_GodownId",
                        column: x => x.GodownId,
                        principalSchema: "krishilink",
                        principalTable: "Godowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentBookings",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    FarmerId = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectReason = table.Column<string>(type: "text", nullable: true),
                    RequestedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayoutId = table.Column<int>(type: "integer", nullable: true),
                    AgreedRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AgreedGross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    CompletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedPromoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PointsUsed = table.Column<int>(type: "integer", nullable: false),
                    PointsEarned = table.Column<int>(type: "integer", nullable: false),
                    PointsAwarded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentBookings_AspNetUsers_FarmerId",
                        column: x => x.FarmerId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentBookings_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalSchema: "krishilink",
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EquipmentBookings_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "krishilink",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EquipmentBookings_Transactions_PayoutId",
                        column: x => x.PayoutId,
                        principalSchema: "krishilink",
                        principalTable: "Transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GodownBookings",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GodownId = table.Column<int>(type: "integer", nullable: false),
                    FarmerId = table.Column<string>(type: "text", nullable: false),
                    StorageTons = table.Column<double>(type: "double precision", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectReason = table.Column<string>(type: "text", nullable: true),
                    RequestedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayoutId = table.Column<int>(type: "integer", nullable: true),
                    AgreedRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AgreedGross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    CompletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedPromoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PointsUsed = table.Column<int>(type: "integer", nullable: false),
                    PointsEarned = table.Column<int>(type: "integer", nullable: false),
                    PointsAwarded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GodownBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GodownBookings_AspNetUsers_FarmerId",
                        column: x => x.FarmerId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GodownBookings_Godowns_GodownId",
                        column: x => x.GodownId,
                        principalSchema: "krishilink",
                        principalTable: "Godowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GodownBookings_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "krishilink",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GodownBookings_Transactions_PayoutId",
                        column: x => x.PayoutId,
                        principalSchema: "krishilink",
                        principalTable: "Transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmerId = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerReply = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerRepliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BookingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: true),
                    GodownId = table.Column<int>(type: "integer", nullable: true),
                    EquipmentBookingId = table.Column<int>(type: "integer", nullable: true),
                    GodownBookingId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_AspNetUsers_FarmerId",
                        column: x => x.FarmerId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_EquipmentBookings_EquipmentBookingId",
                        column: x => x.EquipmentBookingId,
                        principalSchema: "krishilink",
                        principalTable: "EquipmentBookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalSchema: "krishilink",
                        principalTable: "Equipment",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_GodownBookings_GodownBookingId",
                        column: x => x.GodownBookingId,
                        principalSchema: "krishilink",
                        principalTable: "GodownBookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Godowns_GodownId",
                        column: x => x.GodownId,
                        principalSchema: "krishilink",
                        principalTable: "Godowns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "krishilink",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "krishilink",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "krishilink",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "krishilink",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "krishilink",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "krishilink",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsVerified",
                schema: "krishilink",
                table: "AspNetUsers",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                schema: "krishilink",
                table: "AspNetUsers",
                column: "PhoneNumber",
                unique: true,
                filter: "\"PhoneNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_VerificationStatus",
                schema: "krishilink",
                table: "AspNetUsers",
                column: "VerificationStatus");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "krishilink",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingExpenses_OwnerId_BookingType",
                schema: "krishilink",
                table: "BookingExpenses",
                columns: new[] { "OwnerId", "BookingType" });

            migrationBuilder.CreateIndex(
                name: "IX_CropCalendarEntries_Category",
                schema: "krishilink",
                table: "CropCalendarEntries",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CropCalendarEntries_ProfileCropName",
                schema: "krishilink",
                table: "CropCalendarEntries",
                column: "ProfileCropName");

            migrationBuilder.CreateIndex(
                name: "IX_CropCalendarEntries_Season",
                schema: "krishilink",
                table: "CropCalendarEntries",
                column: "Season");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_District",
                schema: "krishilink",
                table: "Equipment",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Latitude_Longitude",
                schema: "krishilink",
                table: "Equipment",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_OwnerId",
                schema: "krishilink",
                table: "Equipment",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBlockedDates_EquipmentId_Date",
                schema: "krishilink",
                table: "EquipmentBlockedDates",
                columns: new[] { "EquipmentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_EquipmentId_Status",
                schema: "krishilink",
                table: "EquipmentBookings",
                columns: new[] { "EquipmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_FarmerId",
                schema: "krishilink",
                table: "EquipmentBookings",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_PaymentId",
                schema: "krishilink",
                table: "EquipmentBookings",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBookings_PayoutId",
                schema: "krishilink",
                table: "EquipmentBookings",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentMaintenanceRecords_EquipmentId",
                schema: "krishilink",
                table: "EquipmentMaintenanceRecords",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentMaintenanceRecords_EquipmentId_ServiceDate",
                schema: "krishilink",
                table: "EquipmentMaintenanceRecords",
                columns: new[] { "EquipmentId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GodownBlockedDates_GodownId_Date",
                schema: "krishilink",
                table: "GodownBlockedDates",
                columns: new[] { "GodownId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_FarmerId",
                schema: "krishilink",
                table: "GodownBookings",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_GodownId_Status",
                schema: "krishilink",
                table: "GodownBookings",
                columns: new[] { "GodownId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_PaymentId",
                schema: "krishilink",
                table: "GodownBookings",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_GodownBookings_PayoutId",
                schema: "krishilink",
                table: "GodownBookings",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_District",
                schema: "krishilink",
                table: "Godowns",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_Latitude_Longitude",
                schema: "krishilink",
                table: "Godowns",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_OwnerId",
                schema: "krishilink",
                table: "Godowns",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_BookingType_BookingId",
                schema: "krishilink",
                table: "LedgerEntries",
                columns: new[] { "BookingType", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_CreditAccount",
                schema: "krishilink",
                table: "LedgerEntries",
                column: "CreditAccount");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_DebitAccount",
                schema: "krishilink",
                table: "LedgerEntries",
                column: "DebitAccount");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_PaymentId",
                schema: "krishilink",
                table: "LedgerEntries",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_PayoutId",
                schema: "krishilink",
                table: "LedgerEntries",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointTransactions_PromoCode",
                schema: "krishilink",
                table: "LoyaltyPointTransactions",
                column: "PromoCode");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointTransactions_UserId",
                schema: "krishilink",
                table: "LoyaltyPointTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointTransactions_UserId_CreatedAt",
                schema: "krishilink",
                table: "LoyaltyPointTransactions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                schema: "krishilink",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_DedupeKey",
                schema: "krishilink",
                table: "Notifications",
                columns: new[] { "UserId", "DedupeKey" },
                unique: true,
                filter: "\"DedupeKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                schema: "krishilink",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingType_BookingId",
                schema: "krishilink",
                table: "Payments",
                columns: new[] { "BookingType", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_FarmerId",
                schema: "krishilink",
                table: "Payments",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayReference",
                schema: "krishilink",
                table: "Payments",
                column: "GatewayReference");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Reference",
                schema: "krishilink",
                table: "Payments",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_EquipmentBookingId",
                schema: "krishilink",
                table: "Reviews",
                column: "EquipmentBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_EquipmentId_CreatedAt",
                schema: "krishilink",
                table: "Reviews",
                columns: new[] { "EquipmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_FarmerId",
                schema: "krishilink",
                table: "Reviews",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_GodownBookingId",
                schema: "krishilink",
                table: "Reviews",
                column: "GodownBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_GodownId_CreatedAt",
                schema: "krishilink",
                table: "Reviews",
                columns: new[] { "GodownId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Reference",
                schema: "krishilink",
                table: "Transactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Status_TransactionDate",
                schema: "krishilink",
                table: "Transactions",
                columns: new[] { "Status", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId",
                schema: "krishilink",
                table: "Transactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_SubmittedAt",
                schema: "krishilink",
                table: "VerificationRequests",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_UserId",
                schema: "krishilink",
                table: "VerificationRequests",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRequests_UserId_Status",
                schema: "krishilink",
                table: "VerificationRequests",
                columns: new[] { "UserId", "Status" });

            // Browser API roles must never bypass the MVC authorization layer.
            migrationBuilder.Sql("""
                REVOKE ALL ON SCHEMA krishilink FROM PUBLIC;
                REVOKE ALL ON ALL TABLES IN SCHEMA krishilink FROM PUBLIC;
                REVOKE ALL ON ALL SEQUENCES IN SCHEMA krishilink FROM PUBLIC;
                DO $permissions$
                DECLARE api_role text;
                BEGIN
                    FOR api_role IN SELECT rolname FROM pg_roles WHERE rolname IN ('anon', 'authenticated')
                    LOOP
                        EXECUTE format('REVOKE ALL ON SCHEMA krishilink FROM %I', api_role);
                        EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA krishilink FROM %I', api_role);
                        EXECUTE format('REVOKE ALL ON ALL SEQUENCES IN SCHEMA krishilink FROM %I', api_role);
                        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA krishilink REVOKE ALL ON TABLES FROM %I', api_role);
                        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA krishilink REVOKE ALL ON SEQUENCES FROM %I', api_role);
                    END LOOP;
                END
                $permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "BookingExpenses",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "CropCalendarEntries",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "CropRecommendations",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "Crops",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "EquipmentBlockedDates",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "EquipmentMaintenanceRecords",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "GodownBlockedDates",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "LedgerEntries",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "LoyaltyPointTransactions",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "Reviews",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "VerificationRequests",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "WeatherData",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "EquipmentBookings",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "GodownBookings",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "Equipment",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "Godowns",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "Transactions",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "krishilink");
        }
    }
}
