using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailDeliveryLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailDeliveryLogs",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipientHash = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    HadAttachment = table.Column<bool>(type: "boolean", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveryLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_AttemptedAt",
                schema: "krishilink",
                table: "EmailDeliveryLogs",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_RecipientHash_AttemptedAt",
                schema: "krishilink",
                table: "EmailDeliveryLogs",
                columns: new[] { "RecipientHash", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_UserId_AttemptedAt",
                schema: "krishilink",
                table: "EmailDeliveryLogs",
                columns: new[] { "UserId", "AttemptedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailDeliveryLogs",
                schema: "krishilink");
        }
    }
}
