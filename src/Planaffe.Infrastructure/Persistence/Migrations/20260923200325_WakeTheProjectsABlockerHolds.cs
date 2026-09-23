using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A change to an issue wakes the projects of the issues it blocks as well
    /// as its own. A blocker may sit in another project (<c>docs/storage.md</c>,
    /// Blockers), and closing or deleting it makes an issue there workable, so a
    /// waiter in that project slept to its deadline while there was work. The
    /// same holds for deleting a whole project, whose issues stop blocking at
    /// that moment.
    /// </summary>
    public partial class WakeTheProjectsABlockerHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                create function planaffe_notify_issue() returns trigger language plpgsql as $$
                declare
                    held uuid;
                begin
                    perform pg_notify('planaffe_' || replace(new.project_id::text, '-', ''), '');
                    for held in
                        select distinct blocked.project_id
                          from blocker edge
                          join issue blocked on blocked.id = edge.blocked_id
                         where edge.blocker_id = new.id
                           and blocked.project_id <> new.project_id
                    loop
                        perform pg_notify('planaffe_' || replace(held::text, '-', ''), '');
                    end loop;
                    return null;
                end $$;

                create function planaffe_notify_project() returns trigger language plpgsql as $$
                declare
                    held uuid;
                begin
                    for held in
                        select distinct blocked.project_id
                          from issue blocking
                          join blocker edge on edge.blocker_id = blocking.id
                          join issue blocked on blocked.id = edge.blocked_id
                         where blocking.project_id = new.id
                           and blocked.project_id <> new.id
                    loop
                        perform pg_notify('planaffe_' || replace(held::text, '-', ''), '');
                    end loop;
                    return null;
                end $$;

                drop trigger issue_notify on issue;
                create trigger issue_notify
                    after insert or update on issue
                    for each row execute function planaffe_notify_issue();

                create trigger project_notify
                    after update of deleted_at on project
                    for each row when (old.deleted_at is distinct from new.deleted_at)
                    execute function planaffe_notify_project();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                drop trigger project_notify on project;
                drop trigger issue_notify on issue;
                create trigger issue_notify
                    after insert or update on issue
                    for each row execute function planaffe_notify();
                drop function planaffe_notify_project();
                drop function planaffe_notify_issue();
                """);
        }
    }
}
