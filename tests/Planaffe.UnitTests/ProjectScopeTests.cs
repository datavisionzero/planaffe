using Planaffe.Application.Acts;
using Planaffe.Application.Ports;

namespace Planaffe.UnitTests;

/// <summary>
/// docs/codebase.md has every act that loads project content ask the scope
/// itself, so that an adapter other than HTTP — the MCP server to come — cannot
/// step around the door the Api keeps. An act that reaches issues without being
/// handed the scope cannot be asking it.
/// </summary>
public sealed class ProjectScopeTests
{
    [Fact]
    public void Every_act_that_reaches_issues_is_handed_the_project_scope()
    {
        var acts = typeof(ProjectScope).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true } && type.Namespace == typeof(ProjectScope).Namespace)
            .Where(type => type.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IIssues))));

        var without = acts
            .Where(type => !type.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(ProjectScope))))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        Assert.Empty(without);
    }
}
