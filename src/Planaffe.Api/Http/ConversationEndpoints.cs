using Planaffe.Application.Acts;

namespace Planaffe.Api.Http;

/// <summary>
/// Comments, questions and the history (<c>docs/api.md</c>): what hangs on an
/// issue beside its fields.
/// </summary>
public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversation(this IEndpointRouteBuilder endpoints)
    {
        var issues = endpoints.MapGroup("/issues/{key}")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        issues.MapPost("/comments", async (string key, CommentRequest? request, CommentOnIssue comment, CancellationToken cancellationToken) =>
            {
                var written = await comment.ExecuteAsync(key, request?.Body, cancellationToken);
                return Results.Created($"/issues/{key}", written);
            })
            .WithName("CommentOnIssue")
            .WithSummary("A note that forces nobody to act. On any issue, by anyone, claimed or not.")
            .Produces<CommentShape>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        issues.MapPost("/questions", async (string key, AskRequest? request, AskQuestion ask, CancellationToken cancellationToken) =>
            {
                var asked = await ask.ExecuteAsync(key, request?.Question, cancellationToken);
                return Results.Created($"/questions/{asked.Id}", asked);
            })
            .WithName("AskQuestion")
            .WithSummary("Ask: what somebody needs to know before the work can go on. Does not release the claim.")
            .Produces<QuestionShape>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        issues.MapGet("/history", (string key, ReadHistory read, CancellationToken cancellationToken) => read.ExecuteAsync(key, cancellationToken))
            .WithName("ReadHistory")
            .WithSummary("Every change to the issue, oldest first: who, when, which field, from what to what.");

        var questions = endpoints.MapGroup("/questions")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        questions.MapGet(string.Empty, (string? project, bool? open, string? issue, string? cursor, int? limit, ListQuestions list, CancellationToken cancellationToken) =>
                list.ExecuteAsync(new QuestionListRequest(project, open, issue, cursor, limit), cancellationToken))
            .WithName("ListQuestions")
            .WithSummary("Questions across the project with their issue, oldest first; open ones by default.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        questions.MapGet("/{id:guid}", (Guid id, int? wait, ReadQuestion read, CancellationToken cancellationToken) =>
                read.ExecuteAsync(id, new ReadQuestionRequest(wait), cancellationToken))
            .WithName("ReadQuestion")
            .WithSummary("One question; with wait, return when it is answered or the deadline passes.")
            .Produces<QuestionShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        questions.MapPost("/{id:guid}/answer", (Guid id, AnswerRequest? request, AnswerQuestion answer, CancellationToken cancellationToken) =>
                answer.ExecuteAsync(id, request?.Answer, cancellationToken))
            .WithName("AnswerQuestion")
            .WithSummary("Answer an open question; a second answer is refused.")
            .Produces<QuestionShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
