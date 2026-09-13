using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The project's wiki is withdrawn (VISION 18, ADR 0027), and this is the
    /// way across. Two wikis side by side would be two addresses, two searches
    /// and two places to look for one sentence; what was a project page becomes
    /// a page in a space, and only then do the tables go.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is written by hand for the same reason the migration before it was:
    /// the order is the point. Every page is carried into a space before
    /// anything is dropped, so there is no state in which the text is gone and
    /// nobody has it. The product has no installation in the wild, so this may
    /// be a one-off and plain — it is not a mechanism, it is a move.
    /// </para>
    /// <para>
    /// One thing is lost and it is named rather than hidden: a page's labels. A
    /// space page carries none, because a label is defined per project and a
    /// space has none (<c>CONTEXT.md</c>, Space page). That is a consequence of
    /// the bracket rather than something forgotten.
    /// </para>
    /// </remarks>
    public partial class WithdrawProjectPages : Migration
    {
        /// <summary>
        /// A space per project that has pages, the project's access carried
        /// over to it, the pages hung directly under it, and their history with
        /// them. A space is a human's to create everywhere else (ADR 0015);
        /// this is not an agent opening a bracket but an instance carrying what
        /// it already holds, and the access it copies is the access those pages
        /// already had.
        /// </summary>
        private const string CarryPagesIntoSpaces =
            """
            do $$
            declare
                p            record;
                target_space uuid;
                candidate    text;
                attempt      int;
            begin
                for p in
                    select pr.id, pr.key, pr.name, pr.created_by, pr.created_at, pr.instructions
                      from project pr
                     where exists (select 1 from page pg where pg.project_id = pr.id)
                     order by pr.key
                loop
                    -- The project key, lower case, is the space's name. A name
                    -- is unique across the instance, so a space that is already
                    -- called that takes the plain one and this project counts up.
                    candidate := lower(p.key);
                    attempt := 1;
                    while exists (select 1 from space s where s.name = candidate) loop
                        attempt := attempt + 1;
                        candidate := lower(p.key) || attempt::text;
                    end loop;

                    target_space := gen_random_uuid();
                    insert into space (id, name, title, closed_to_agents, created_by, created_at, updated_at)
                         values (target_space, candidate, p.name, false, p.created_by, p.created_at, p.created_at);

                    -- Whoever reached the project reached its pages, the agent
                    -- included, and nobody loses that here: the grants are
                    -- copied as they stand and the space is open to agents.
                    insert into space_access (space_id, user_id, granted_by, granted_at)
                         select target_space, pa.user_id, pa.granted_by, pa.granted_at
                           from project_access pa
                          where pa.project_id = p.id;

                    -- Directly under the space, keeping the id, because the
                    -- history hangs on it. A page that is soft-deleted travels
                    -- deleted: a migration that dropped it would end its grace
                    -- period without telling anybody (ADR 0013).
                    --
                    -- The one page that does not travel is the one the
                    -- instructions were taken out of. Its text is on the
                    -- project now, and the same sentence in two places is what
                    -- this withdrawal is written against. It is recognised by
                    -- its body, because that is exactly how the migration
                    -- before this one filled the column.
                    insert into space_page (id, space_id, parent_id, depth, slug, title, body,
                                            created_by, created_at, updated_by, updated_at,
                                            deleted_at, deleted_by, deleted_with)
                         select pg.id, target_space, null, 0, pg.slug, pg.title, pg.body,
                                pg.created_by, pg.created_at, pg.updated_by, pg.updated_at,
                                pg.deleted_at, pg.deleted_by, null
                           from page pg
                          where pg.project_id = p.id
                            and not (pg.deleted_at is null
                                     and p.instructions is not null
                                     and btrim(pg.body) = p.instructions);
                end loop;
            end
            $$;
            """;

        /// <summary>
        /// The history follows its page. What is left afterwards pointed at the
        /// page the instructions came out of, and it is deleted rather than left
        /// dangling — the foreign key that would have taken it along is dropped
        /// a few statements below, and a row pointing at nothing would fail the
        /// new check constraint with a name nobody could trace back.
        /// </summary>
        private const string CarryHistoryAcross =
            """
            update history
               set space_page_id = page_id,
                   page_id = null
             where page_id is not null
               and exists (select 1 from space_page sp where sp.id = history.page_id);

            delete from history where page_id is not null;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CarryPagesIntoSpaces);
            migrationBuilder.Sql(CarryHistoryAcross);

            migrationBuilder.DropForeignKey(
                name: "fk_history_page",
                table: "history");

            migrationBuilder.DropTable(
                name: "page_label");

            migrationBuilder.DropTable(
                name: "page");

            migrationBuilder.DropIndex(
                name: "history_page",
                table: "history");

            migrationBuilder.DropCheckConstraint(
                name: "ck_history_subject",
                table: "history");

            migrationBuilder.DropColumn(
                name: "page_id",
                table: "history");

            migrationBuilder.AddCheckConstraint(
                name: "ck_history_subject",
                table: "history",
                sql: "num_nonnulls(issue_id, epic_id, space_page_id) = 1");
        }

        /// <summary>
        /// The tables come back empty and nothing is carried the other way:
        /// the pages are in spaces now, and a migration only runs forward
        /// (ADR 0011). This is the shape of the schema, not a way back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_history_subject",
                table: "history");

            migrationBuilder.AddColumn<Guid>(
                name: "page_id",
                table: "history",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "page",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    search = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('simple', title || ' ' || body)", stored: true),
                    slug = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page", x => x.id);
                    table.ForeignKey(
                        name: "fk_page_created_by",
                        column: x => x.created_by,
                        principalTable: "identity",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_page_deleted_by",
                        column: x => x.deleted_by,
                        principalTable: "identity",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_page_project",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_page_updated_by",
                        column: x => x.updated_by,
                        principalTable: "identity",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "page_label",
                columns: table => new
                {
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_label", x => new { x.page_id, x.label_id });
                    table.ForeignKey(
                        name: "fk_page_label_label",
                        column: x => x.label_id,
                        principalTable: "label",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_page_label_page",
                        column: x => x.page_id,
                        principalTable: "page",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "history_page",
                table: "history",
                columns: new[] { "page_id", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_history_subject",
                table: "history",
                sql: "num_nonnulls(issue_id, epic_id, page_id, space_page_id) = 1");

            migrationBuilder.CreateIndex(
                name: "page_search",
                table: "page",
                column: "search")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "page_slug",
                table: "page",
                columns: new[] { "project_id", "slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_history_page",
                table: "history",
                column: "page_id",
                principalTable: "page",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
