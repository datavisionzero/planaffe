namespace Planaffe.Api.Http;

/// <summary>
/// Whose address a path is when both claim it: a browser navigating to
/// <c>/spaces</c> wants the application, the application asking about
/// <c>/spaces</c> wants the instance.
/// </summary>
/// <remarks>
/// <para>
/// The API carries no prefix of its own (<c>docs/api.md</c>), and the web
/// application is served from the same origin, so several of its screens stand
/// on a path an endpoint also answers — <c>/projects</c>, <c>/admin/projects</c>
/// and, with the knowledge base, <c>/spaces</c>. Both are <c>GET</c> on the
/// same path. Without this, routing matched the endpoint and whoever opened
/// the address out of a bookmark or a colleague's message got a JSON document
/// where the screen should have been; only a link followed inside the running
/// application worked.
/// </para>
/// <para>
/// What tells the two apart is the browser itself. <c>Sec-Fetch-Dest:
/// document</c> is what a navigation sends and nothing a client library sends,
/// and where the header is absent an <c>Accept</c> that asks for HTML says the
/// same. A path with an extension is never a navigation of this kind — that is
/// a built asset or <c>/openapi/v1.json</c>, and those belong to whoever
/// answers them today.
/// </para>
/// <para>
/// The path is put back before the answer leaves, so that the request log of
/// <c>Program.cs</c> keeps saying what was asked for rather than what was
/// served. The development server does the same thing from the other side
/// (<c>src/web/vite.config.ts</c>), because there the two toolchains stand
/// side by side and the proxy is where the same question is asked.
/// </para>
/// </remarks>
public static class BrowserNavigation
{
    public static IApplicationBuilder UsePlanaffeBrowserNavigation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            if (!IsDocument(context.Request))
            {
                await next(context);
                return;
            }

            var asked = context.Request.Path;
            context.Request.Path = "/index.html";

            try
            {
                await next(context);
            }
            finally
            {
                context.Request.Path = asked;
            }
        });
    }

    private static bool IsDocument(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        var path = request.Path.Value ?? "/";

        if (Path.HasExtension(path))
        {
            return false;
        }

        var destination = request.Headers["Sec-Fetch-Dest"].ToString();

        return destination.Length > 0
            ? destination == "document"
            : request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
