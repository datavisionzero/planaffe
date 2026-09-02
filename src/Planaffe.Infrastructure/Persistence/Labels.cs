using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Projects;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>The label rows of a project.</summary>
public sealed class Labels(PlanaffeDbContext context) : ILabels
{
    public async Task<IReadOnlyList<Label>> ListAsync(Guid projectId, CancellationToken cancellationToken) =>
        await context.Labels
            .Where(l => l.ProjectId == projectId && l.DeletedAt == null)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

    public Task<Label?> FindAsync(Guid projectId, string name, CancellationToken cancellationToken) =>
        context.Labels.SingleOrDefaultAsync(l => l.ProjectId == projectId && l.Name == name, cancellationToken);

    public async Task AddAsync(Label label, CancellationToken cancellationToken)
    {
        context.Labels.Add(label);
        await SaveOrRefuseTheNameAsync(label.Name, cancellationToken);
    }

    public Task SaveAsync(Label label, CancellationToken cancellationToken) =>
        SaveOrRefuseTheNameAsync(label.Name, cancellationToken);

    // The live issues carrying this label and another live label of the group:
    // what a group change would leave with two of one group.
    public async Task<IReadOnlyList<string>> IssuesWithAnotherOfGroupAsync(
        Label label, string groupName, CancellationToken cancellationToken) =>
        await (
            from issue in context.Issues
            where issue.DeletedAt == null
            where context.IssueLabels.Any(il => il.IssueId == issue.Id && il.LabelId == label.Id)
            where (
                from il in context.IssueLabels
                join other in context.Labels on il.LabelId equals other.Id
                where il.IssueId == issue.Id && other.Id != label.Id && other.DeletedAt == null && other.Group == groupName
                select il).Any()
            join project in context.Projects on issue.ProjectId equals project.Id
            orderby issue.Number
            select project.Key + "-" + issue.Number).ToListAsync(cancellationToken);

    private async Task SaveOrRefuseTheNameAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException collision) when (collision.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "label_name",
        })
        {
            throw Refusal.Validation("name", $"The label {name} exists in this project.");
        }
    }
}
