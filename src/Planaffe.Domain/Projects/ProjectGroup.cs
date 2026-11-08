namespace Planaffe.Domain.Projects;

public sealed class ProjectGroup
{
    private ProjectGroup() { }
    public Guid Id { get; private init; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public static ProjectGroup Create(string name, string? description)
    {
        var group = new ProjectGroup { Id = Guid.CreateVersion7() };
        group.Change(name, description);
        return group;
    }
    public void Change(string name, string? description)
    {
        Name = Project.NormalizeName(name);
        var text = description?.Trim();
        if (text?.Length > 500) throw new ArgumentException("A group description is at most 500 characters.");
        Description = string.IsNullOrEmpty(text) ? null : text;
    }
}
