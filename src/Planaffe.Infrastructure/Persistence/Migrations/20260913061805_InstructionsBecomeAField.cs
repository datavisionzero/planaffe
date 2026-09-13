using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The instructions stop being a page the project points at and become a
    /// text on the project (VISION 15.3, 18., ADR 0027). The order below is the
    /// whole point of writing this one by hand: the column arrives, the text is
    /// carried into it, and only then does the pointer go — so that there is no
    /// moment in which the instructions exist nowhere.
    /// </summary>
    /// <remarks>
    /// The page itself stays where it is. It leaves in <c>WithdrawProjectPages</c>
    /// with the rest of the project's wiki, and it is the one page that does not
    /// travel into a space: its text is here now, and the same sentence in two
    /// places is what the withdrawal is written against.
    /// </remarks>
    public partial class InstructionsBecomeAField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "instructions",
                table: "project",
                type: "text",
                nullable: true);

            // A deleted page delivered nothing while it was designated, and it
            // delivers nothing now: `deleted_at is null` keeps the move faithful
            // to what a ticket would have carried the moment before it ran. An
            // empty body becomes no instructions at all, which is what the
            // domain does with a blank text from here on.
            migrationBuilder.Sql(
                """
                update project
                   set instructions = nullif(btrim(page.body), '')
                  from page
                 where page.id = project.instructions_page_id
                   and page.deleted_at is null
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_project_instructions_page",
                table: "project");

            migrationBuilder.DropColumn(
                name: "instructions_page_id",
                table: "project");
        }

        /// <summary>
        /// The schema goes back and the text does not: it was a page's and the
        /// pointer that named which page is gone. Migrations only run forward
        /// (ADR 0011), so this is the shape of the table and not a way back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "instructions_page_id",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_project_instructions_page",
                table: "project",
                column: "instructions_page_id",
                principalTable: "page",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropColumn(
                name: "instructions",
                table: "project");
        }
    }
}
