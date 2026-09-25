using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityBookmarks",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityBookmarks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityComments",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    AttachmentImageUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AudioRecordingUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AudioDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ParentCommentId = table.Column<int>(type: "integer", nullable: true),
                    IsAcceptedSolution = table.Column<bool>(type: "boolean", nullable: false),
                    UpvoteCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityComments_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityComments_CommunityComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalSchema: "krishilink",
                        principalTable: "CommunityComments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommunityPosts",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    PostType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Experience"),
                    CropCategory = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IssueCategory = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    UrgencyLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    CropAge = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AffectedArea = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    District = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Upazila = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsHelpRequest = table.Column<bool>(type: "boolean", nullable: false),
                    IsSolved = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptedCommentId = table.Column<int>(type: "integer", nullable: true),
                    AudioRecordingUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AudioDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    LikeCount = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    ShareCount = table.Column<int>(type: "integer", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsFlagged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPosts_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityPosts_CommunityComments_AcceptedCommentId",
                        column: x => x.AcceptedCommentId,
                        principalSchema: "krishilink",
                        principalTable: "CommunityComments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommunityPostReports",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReporterId = table.Column<string>(type: "text", nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPostReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPostReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityPostReports_CommunityPosts_PostId",
                        column: x => x.PostId,
                        principalSchema: "krishilink",
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityReactions",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: true),
                    CommentId = table.Column<int>(type: "integer", nullable: true),
                    ReactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Helpful"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityReactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "krishilink",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityReactions_CommunityComments_CommentId",
                        column: x => x.CommentId,
                        principalSchema: "krishilink",
                        principalTable: "CommunityComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunityReactions_CommunityPosts_PostId",
                        column: x => x.PostId,
                        principalSchema: "krishilink",
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostMedia",
                schema: "krishilink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    MediaUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Image"),
                    ThumbnailUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostMedia_CommunityPosts_PostId",
                        column: x => x.PostId,
                        principalSchema: "krishilink",
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityBookmarks_PostId",
                schema: "krishilink",
                table: "CommunityBookmarks",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityBookmarks_UserId_PostId",
                schema: "krishilink",
                table: "CommunityBookmarks",
                columns: new[] { "UserId", "PostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_AuthorId",
                schema: "krishilink",
                table: "CommunityComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_CreatedAt",
                schema: "krishilink",
                table: "CommunityComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_ParentCommentId",
                schema: "krishilink",
                table: "CommunityComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_PostId",
                schema: "krishilink",
                table: "CommunityComments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPostReports_PostId",
                schema: "krishilink",
                table: "CommunityPostReports",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPostReports_ReporterId",
                schema: "krishilink",
                table: "CommunityPostReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPostReports_Status",
                schema: "krishilink",
                table: "CommunityPostReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_AcceptedCommentId",
                schema: "krishilink",
                table: "CommunityPosts",
                column: "AcceptedCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_AuthorId",
                schema: "krishilink",
                table: "CommunityPosts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_CreatedAt",
                schema: "krishilink",
                table: "CommunityPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_District",
                schema: "krishilink",
                table: "CommunityPosts",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_IsHelpRequest",
                schema: "krishilink",
                table: "CommunityPosts",
                column: "IsHelpRequest");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_IsSolved",
                schema: "krishilink",
                table: "CommunityPosts",
                column: "IsSolved");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_PostType",
                schema: "krishilink",
                table: "CommunityPosts",
                column: "PostType");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_UrgencyLevel",
                schema: "krishilink",
                table: "CommunityPosts",
                column: "UrgencyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_CommentId",
                schema: "krishilink",
                table: "CommunityReactions",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_PostId",
                schema: "krishilink",
                table: "CommunityReactions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_UserId_PostId_CommentId",
                schema: "krishilink",
                table: "CommunityReactions",
                columns: new[] { "UserId", "PostId", "CommentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_PostId",
                schema: "krishilink",
                table: "PostMedia",
                column: "PostId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityBookmarks_CommunityPosts_PostId",
                schema: "krishilink",
                table: "CommunityBookmarks",
                column: "PostId",
                principalSchema: "krishilink",
                principalTable: "CommunityPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityComments_CommunityPosts_PostId",
                schema: "krishilink",
                table: "CommunityComments",
                column: "PostId",
                principalSchema: "krishilink",
                principalTable: "CommunityPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunityComments_CommunityPosts_PostId",
                schema: "krishilink",
                table: "CommunityComments");

            migrationBuilder.DropTable(
                name: "CommunityBookmarks",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "CommunityPostReports",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "CommunityReactions",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "PostMedia",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "CommunityPosts",
                schema: "krishilink");

            migrationBuilder.DropTable(
                name: "CommunityComments",
                schema: "krishilink");
        }
    }
}
