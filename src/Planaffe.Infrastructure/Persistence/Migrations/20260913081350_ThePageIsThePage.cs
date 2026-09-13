using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The prefix goes. <c>space_page</c> was called that because there were two
    /// pages and a name had to say which; the project's wiki is withdrawn
    /// (VISION 18), the name <c>page</c> is free, and <c>CONTEXT.md</c> has one
    /// entry for it again.
    /// </summary>
    /// <remarks>
    /// Every statement is a rename and nothing is dropped: the rows are the
    /// knowledge base, and a scaffolded drop-and-create would have taken them
    /// with it. Postgres does not carry a table's constraints and indexes along
    /// when the table is renamed, so each is named here — and the check on
    /// <c>history</c> is not among them, because renaming a column rewrites the
    /// expressions that depend on it.
    /// </remarks>
    public partial class ThePageIsThePage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                alter table space_page rename to page;

                alter table page rename constraint pk_space_page            to pk_page;
                alter table page rename constraint ck_space_page_depth      to ck_page_depth;
                alter table page rename constraint fk_space_page_space      to fk_page_space;
                alter table page rename constraint fk_space_page_parent     to fk_page_parent;
                alter table page rename constraint fk_space_page_created_by to fk_page_created_by;
                alter table page rename constraint fk_space_page_updated_by to fk_page_updated_by;
                alter table page rename constraint fk_space_page_deleted_by to fk_page_deleted_by;

                alter index space_page_slug   rename to page_slug;
                alter index space_page_search rename to page_search;

                alter table history rename column space_page_id to page_id;
                alter table history rename constraint fk_history_space_page to fk_history_page;
                alter index history_space_page rename to history_page;
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                alter index history_page rename to history_space_page;
                alter table history rename constraint fk_history_page to fk_history_space_page;
                alter table history rename column page_id to space_page_id;

                alter index page_search rename to space_page_search;
                alter index page_slug   rename to space_page_slug;

                alter table page rename constraint fk_page_deleted_by to fk_space_page_deleted_by;
                alter table page rename constraint fk_page_updated_by to fk_space_page_updated_by;
                alter table page rename constraint fk_page_created_by to fk_space_page_created_by;
                alter table page rename constraint fk_page_parent     to fk_space_page_parent;
                alter table page rename constraint fk_page_space      to fk_space_page_space;
                alter table page rename constraint ck_page_depth      to ck_space_page_depth;
                alter table page rename constraint pk_page            to pk_space_page;

                alter table page rename to space_page;
                """);
    }
}
