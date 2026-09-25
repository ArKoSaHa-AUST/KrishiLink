using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSuggestionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuggestionStates",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SuggestionKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuggestionStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuggestionStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuggestionStates_UserId_SuggestionKey",
                schema: "krishilink",
                table: "SuggestionStates",
                columns: new[] { "UserId", "SuggestionKey" },
                unique: true);

            // Per-user state: never reachable through Supabase's browser API roles.
            migrationBuilder.Sql("""
                REVOKE ALL ON krishilink."SuggestionStates" FROM PUBLIC;
                DO $permissions$
                DECLARE api_role text;
                BEGIN
                    FOR api_role IN SELECT rolname FROM pg_roles WHERE rolname IN ('anon', 'authenticated')
                    LOOP
                        EXECUTE format('REVOKE ALL ON krishilink."SuggestionStates" FROM %I', api_role);
                    END LOOP;
                END
                $permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuggestionStates",
                schema: "krishilink");
        }
    }
}
