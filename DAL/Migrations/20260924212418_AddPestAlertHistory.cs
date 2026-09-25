using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPestAlertHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PestAlertHistories",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    District = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RuleId = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RiskPercentage = table.Column<double>(type: "double precision", nullable: false),
                    TriggeredOn = table.Column<DateTime>(type: "date", nullable: false),
                    WeatherSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestAlertHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PestAlertFeedback",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PestAlertHistoryId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    IsAccurate = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestAlertFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PestAlertFeedback_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PestAlertFeedback_PestAlertHistories_PestAlertHistoryId",
                        column: x => x.PestAlertHistoryId,
                        principalSchema: "krishilink",
                        principalTable: "PestAlertHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertFeedback_PestAlertHistoryId_UserId",
                schema: "krishilink",
                table: "PestAlertFeedback",
                columns: new[] { "PestAlertHistoryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertFeedback_UserId",
                schema: "krishilink",
                table: "PestAlertFeedback",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertHistories_District_RuleId_TriggeredOn",
                schema: "krishilink",
                table: "PestAlertHistories",
                columns: new[] { "District", "RuleId", "TriggeredOn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertHistories_TriggeredOn",
                schema: "krishilink",
                table: "PestAlertHistories",
                column: "TriggeredOn");

            // Reached only through the app, never through Supabase's browser API roles.
            migrationBuilder.Sql("""
                REVOKE ALL ON krishilink."PestAlertHistories", krishilink."PestAlertFeedback" FROM PUBLIC;
                DO $permissions$
                DECLARE api_role text;
                BEGIN
                    FOR api_role IN SELECT rolname FROM pg_roles WHERE rolname IN ('anon', 'authenticated')
                    LOOP
                        EXECUTE format('REVOKE ALL ON krishilink."PestAlertHistories", krishilink."PestAlertFeedback" FROM %I', api_role);
                    END LOOP;
                END
                $permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PestAlertFeedback",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "PestAlertHistories",
                schema: "krishilink");
        }
    }
}
