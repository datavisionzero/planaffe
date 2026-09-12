using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpacePages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "space_page",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_with = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_space_page", x => x.id);
                    table.CheckConstraint("ck_space_page_depth", "depth >= 0 and depth <= 2");
                    table.ForeignKey(
                        name: "fk_space_page_created_by",
                        column: x => x.created_by,
                        principalTable: "identity",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_space_page_deleted_by",
                        column: x => x.deleted_by,
                        principalTable: "identity",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_space_page_parent",
                        column: x => x.parent_id,
                        principalTable: "space_page",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_space_page_space",
                        column: x => x.space_id,
                        principalTable: "space",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_space_page_updated_by",
                        column: x => x.updated_by,
                        principalTable: "identity",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "space_page_slug",
                table: "space_page",
                columns: new[] { "space_id", "parent_id", "slug" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "space_page");
        }
    }
}
