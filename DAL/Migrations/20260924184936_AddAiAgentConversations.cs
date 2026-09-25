using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAgentConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentConversations",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentConversations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentMessages",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ToolName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ToolArgumentsJson = table.Column<string>(type: "text", nullable: true),
                    TokensIn = table.Column<int>(type: "integer", nullable: true),
                    TokensOut = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProposalJson = table.Column<string>(type: "text", nullable: true),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposalUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentMessages_AgentConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "krishilink",
                        principalTable: "AgentConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentConversations_UpdatedAt",
                schema: "krishilink",
                table: "AgentConversations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentConversations_UserId_UpdatedAt",
                schema: "krishilink",
                table: "AgentConversations",
                columns: new[] { "UserId", "UpdatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_ConversationId_CreatedAt",
                schema: "krishilink",
                table: "AgentMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_ProposalId",
                schema: "krishilink",
                table: "AgentMessages",
                column: "ProposalId",
                unique: true,
                filter: "\"ProposalId\" IS NOT NULL");

            // Conversation text must never be reachable through Supabase's browser API roles.
            migrationBuilder.Sql("""
                REVOKE ALL ON krishilink."AgentConversations", krishilink."AgentMessages" FROM PUBLIC;
                DO $permissions$
                DECLARE api_role text;
                BEGIN
                    FOR api_role IN SELECT rolname FROM pg_roles WHERE rolname IN ('anon', 'authenticated')
                    LOOP
                        EXECUTE format('REVOKE ALL ON krishilink."AgentConversations", krishilink."AgentMessages" FROM %I', api_role);
                    END LOOP;
                END
                $permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentMessages",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "AgentConversations",
                schema: "krishilink");
        }
    }
}
