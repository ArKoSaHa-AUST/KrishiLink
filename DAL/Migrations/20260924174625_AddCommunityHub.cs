using System;
using Microsoft.EntityFrameworkCore.Migrations;

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
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityBookmarks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentImageUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AudioRecordingUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AudioDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    IsAcceptedSolution = table.Column<bool>(type: "bit", nullable: false),
                    UpvoteCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityComments_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityComments_CommunityComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "CommunityComments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommunityPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Experience"),
                    CropCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IssueCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    UrgencyLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    CropAge = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AffectedArea = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    District = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Upazila = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsHelpRequest = table.Column<bool>(type: "bit", nullable: false),
                    IsSolved = table.Column<bool>(type: "bit", nullable: false),
                    AcceptedCommentId = table.Column<int>(type: "int", nullable: true),
                    AudioRecordingUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AudioDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    LikeCount = table.Column<int>(type: "int", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    ShareCount = table.Column<int>(type: "int", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    IsFlagged = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPosts_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityPosts_CommunityComments_AcceptedCommentId",
                        column: x => x.AcceptedCommentId,
                        principalTable: "CommunityComments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommunityPostReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPostReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPostReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityPostReports_CommunityPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: true),
                    CommentId = table.Column<int>(type: "int", nullable: true),
                    ReactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Helpful"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityReactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityReactions_CommunityComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "CommunityComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunityReactions_CommunityPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    MediaUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Image"),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostMedia_CommunityPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityBookmarks_PostId",
                table: "CommunityBookmarks",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityBookmarks_UserId_PostId",
                table: "CommunityBookmarks",
                columns: new[] { "UserId", "PostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_AuthorId",
                table: "CommunityComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_CreatedAt",
                table: "CommunityComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_ParentCommentId",
                table: "CommunityComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComments_PostId",
                table: "CommunityComments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPostReports_PostId",
                table: "CommunityPostReports",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPostReports_ReporterId",
                table: "CommunityPostReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPostReports_Status",
                table: "CommunityPostReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_AcceptedCommentId",
                table: "CommunityPosts",
                column: "AcceptedCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_AuthorId",
                table: "CommunityPosts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_CreatedAt",
                table: "CommunityPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_District",
                table: "CommunityPosts",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_IsHelpRequest",
                table: "CommunityPosts",
                column: "IsHelpRequest");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_IsSolved",
                table: "CommunityPosts",
                column: "IsSolved");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_PostType",
                table: "CommunityPosts",
                column: "PostType");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_UrgencyLevel",
                table: "CommunityPosts",
                column: "UrgencyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_CommentId",
                table: "CommunityReactions",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_PostId",
                table: "CommunityReactions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReactions_UserId_PostId_CommentId",
                table: "CommunityReactions",
                columns: new[] { "UserId", "PostId", "CommentId" },
                unique: true,
                filter: "[PostId] IS NOT NULL AND [CommentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_PostId",
                table: "PostMedia",
                column: "PostId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityBookmarks_CommunityPosts_PostId",
                table: "CommunityBookmarks",
                column: "PostId",
                principalTable: "CommunityPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityComments_CommunityPosts_PostId",
                table: "CommunityComments",
                column: "PostId",
                principalTable: "CommunityPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunityComments_CommunityPosts_PostId",
                table: "CommunityComments");

            migrationBuilder.DropTable(
                name: "CommunityBookmarks");

            migrationBuilder.DropTable(
                name: "CommunityPostReports");

            migrationBuilder.DropTable(
                name: "CommunityReactions");

            migrationBuilder.DropTable(
                name: "PostMedia");

            migrationBuilder.DropTable(
                name: "CommunityPosts");

            migrationBuilder.DropTable(
                name: "CommunityComments");
        }
    }
}
