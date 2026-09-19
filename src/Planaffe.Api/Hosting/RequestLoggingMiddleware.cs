using System.Diagnostics;

namespace Planaffe.Api.Hosting;

/// <summary>One request line without query strings, headers or bodies.</summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var statusCode = StatusCodes.Status500InternalServerError;

        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms.",
                context.Request.Method,
                context.Request.Path.Value,
                statusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
