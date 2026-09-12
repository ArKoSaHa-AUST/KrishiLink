using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateVerificationPii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NidNumber",
                table: "VerificationRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            // NidLast4 was introduced without an AddColumn, so a fresh database has no column to alter.
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[VerificationRequests]', N'NidLast4') IS NULL
    ALTER TABLE [VerificationRequests] ADD [NidLast4] nvarchar(10) NULL;
ELSE
    ALTER TABLE [VerificationRequests] ALTER COLUMN [NidLast4] nvarchar(10) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NidNumber",
                table: "VerificationRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "NidLast4",
                table: "VerificationRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }
    }
}
