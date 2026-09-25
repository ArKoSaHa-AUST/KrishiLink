using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSupabaseSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupabaseSessions",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProtectedTokens = table.Column<string>(type: "text", nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupabaseSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupabaseSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupabaseSessions_ExpiresAt",
                schema: "krishilink",
                table: "SupabaseSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupabaseSessions_UserId",
                schema: "krishilink",
                table: "SupabaseSessions",
                column: "UserId");

            // Session rows must never be reachable through Supabase's browser API roles.
            migrationBuilder.Sql("""
                REVOKE ALL ON krishilink."SupabaseSessions" FROM PUBLIC;
                DO $permissions$
                DECLARE api_role text;
                BEGIN
                    FOR api_role IN SELECT rolname FROM pg_roles WHERE rolname IN ('anon', 'authenticated')
                    LOOP
                        EXECUTE format('REVOKE ALL ON krishilink."SupabaseSessions" FROM %I', api_role);
                    END LOOP;
                END
                $permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupabaseSessions",
                schema: "krishilink");
        }
    }
}
