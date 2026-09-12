using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpacePageSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search",
                table: "space_page",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('simple', title || ' ' || body)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "space_page_search",
                table: "space_page",
                column: "search")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "space_page_search",
                table: "space_page");

            migrationBuilder.DropColumn(
                name: "search",
                table: "space_page");
        }
    }
}
