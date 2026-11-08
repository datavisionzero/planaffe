using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_group",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_group", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_group_name",
                table: "project_group",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_project_project_group_group_id",
                table: "project",
                column: "group_id",
                principalTable: "project_group",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_project_project_group_group_id",
                table: "project");

            migrationBuilder.DropTable(
                name: "project_group");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "project");
        }
    }
}
