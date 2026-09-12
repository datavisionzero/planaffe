using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HistoryOfSpacePages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_history_subject",
                table: "history");

            migrationBuilder.AddColumn<Guid>(
                name: "space_page_id",
                table: "history",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "history_space_page",
                table: "history",
                columns: new[] { "space_page_id", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_history_subject",
                table: "history",
                sql: "num_nonnulls(issue_id, epic_id, page_id, space_page_id) = 1");

            migrationBuilder.AddForeignKey(
                name: "fk_history_space_page",
                table: "history",
                column: "space_page_id",
                principalTable: "space_page",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_history_space_page",
                table: "history");

            migrationBuilder.DropIndex(
                name: "history_space_page",
                table: "history");

            migrationBuilder.DropCheckConstraint(
                name: "ck_history_subject",
                table: "history");

            migrationBuilder.DropColumn(
                name: "space_page_id",
                table: "history");

            migrationBuilder.AddCheckConstraint(
                name: "ck_history_subject",
                table: "history",
                sql: "num_nonnulls(issue_id, epic_id, page_id) = 1");
        }
    }
}
