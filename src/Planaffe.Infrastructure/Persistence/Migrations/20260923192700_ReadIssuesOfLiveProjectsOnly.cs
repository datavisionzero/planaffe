using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// An issue in a deleted project is absent from <c>issue_read</c> as well
    /// (ADR 0013). Deleting a project marks the project row alone, so the view
    /// has to ask, or every read that is not joined to the project — a blocker
    /// in another project, a release's issues, an epic's progress — keeps
    /// seeing it for the whole grace period.
    /// </summary>
    public partial class ReadIssuesOfLiveProjectsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop view issue_read;");
            CreateReadView(migrationBuilder, liveProjectsOnly: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop view issue_read;");
            CreateReadView(migrationBuilder, liveProjectsOnly: false);
        }

        private static void CreateReadView(MigrationBuilder migrationBuilder, bool liveProjectsOnly)
        {
            var project = liveProjectsOnly
                ? "\n           and not exists (select 1 from project p where p.id = issue.project_id and p.deleted_at is not null)"
                : string.Empty;
            migrationBuilder.Sql($$"""
                create view issue_read as
                select i.id, i.project_id, i.number, i.title, i.description, i.result,
                       case when i.claim_expired then 'todo' else i.status end as status,
                       i.ready, i.priority, i.assignee_id, i.epic_id, i.parent_id,
                       case when i.claim_expired then null else i.claimed_by end        as claimed_by,
                       case when i.claim_expired then null else i.claimed_at end        as claimed_at,
                       case when i.claim_expired then null else i.claim_expires_at end  as claim_expires_at,
                       i.author_id, i.created_at, i.updated_at, i.closed_at
                  from (select *,
                               claimed_by is not null
                           and claim_expires_at is not null
                           and claim_expires_at <= now() as claim_expired
                          from issue
                         where deleted_at is null{{project}}) i;
                """);
        }
    }
}
