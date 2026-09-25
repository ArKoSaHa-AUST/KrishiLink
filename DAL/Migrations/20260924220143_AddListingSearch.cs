using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace KrishiLink.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddListingSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                schema: "krishilink",
                table: "Godowns",
                type: "tsvector",
                nullable: false)
                .Annotation("Npgsql:TsVectorConfig", "simple")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Name", "StorageType", "Description", "District", "Facilities" });

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                schema: "krishilink",
                table: "Equipment",
                type: "tsvector",
                nullable: false)
                .Annotation("Npgsql:TsVectorConfig", "simple")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Name", "Category", "Description", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_Description_trgm",
                schema: "krishilink",
                table: "Godowns",
                column: "Description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_Name_trgm",
                schema: "krishilink",
                table: "Godowns",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_SearchVector",
                schema: "krishilink",
                table: "Godowns",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Description_trgm",
                schema: "krishilink",
                table: "Equipment",
                column: "Description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Name_trgm",
                schema: "krishilink",
                table: "Equipment",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_SearchVector",
                schema: "krishilink",
                table: "Equipment",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Godowns_Description_trgm",
                schema: "krishilink",
                table: "Godowns");

            migrationBuilder.DropIndex(
                name: "IX_Godowns_Name_trgm",
                schema: "krishilink",
                table: "Godowns");

            migrationBuilder.DropIndex(
                name: "IX_Godowns_SearchVector",
                schema: "krishilink",
                table: "Godowns");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_Description_trgm",
                schema: "krishilink",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_Name_trgm",
                schema: "krishilink",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_SearchVector",
                schema: "krishilink",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                schema: "krishilink",
                table: "Godowns");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                schema: "krishilink",
                table: "Equipment");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
