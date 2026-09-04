using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Projects;

namespace Planaffe.Api.Http;

/// <summary>
/// The single HTTP door for project content. Acts still constrain collection
/// queries; this guard makes every direct-key route indistinguishable from an
/// unknown key before its handler loads content.
/// </summary>
public sealed class ProjectScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ProjectScope scope, IProjects projects, IIssues issues)
    {
        if (http.User.Identity?.IsAuthenticated == true && ProjectId(http, projects, issues) is { } projectId)
            await scope.RequireAsync(await projectId, http.RequestAborted);

        await next(http);
    }

    private static Task<Guid>? ProjectId(HttpContext http, IProjects projects, IIssues issues)
    {
        var segments = http.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (segments.Length < 2) return null;

        // Project assignment administration and lifecycle are explicit
        // administrator surfaces, not project content.
        if (segments[0] == "projects" && (segments.Length >= 3 && segments[2] == "users"
            || http.Request.Method == "DELETE" && segments.Length == 2
            || segments.Length == 3 && segments[2] == "restore")) return null;

        if (segments[0] == "projects") return FromProjectKey(projects, segments[1], http.RequestAborted);
        if (segments[0] == "issues" && IssueKey.TryParse(segments[1], out var issueProject, out _))
            return FromProjectKey(projects, issueProject, http.RequestAborted);
        if (segments[0] == "epics" && EpicKey.TryParse(segments[1], out var epicProject, out _))
            return FromProjectKey(projects, epicProject, http.RequestAborted);
        if (segments[0] == "questions" && Guid.TryParse(segments[1], out var questionId))
            return FromQuestion(issues, questionId, http.RequestAborted);
        return null;
    }

    private static async Task<Guid> FromProjectKey(IProjects projects, string key, CancellationToken cancellationToken) =>
        (await projects.FindByKeyAsync(key.Trim().ToUpperInvariant(), cancellationToken))?.Id ?? Guid.Empty;

    private static async Task<Guid> FromQuestion(IIssues issues, Guid id, CancellationToken cancellationToken) =>
        (await issues.FindQuestionAsync(id, cancellationToken))?.ProjectId ?? Guid.Empty;
}
