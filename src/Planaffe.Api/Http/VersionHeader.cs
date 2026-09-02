using Planaffe.Api.Hosting;

namespace Planaffe.Api.Http;

/// <summary>
/// <c>Planaffe-Version</c> on every response (ADR 0011), the refused and the
/// failed ones included: skew is reported by the CLI from whatever answer it
/// got, and a 401 is an answer.
/// </summary>
public static class VersionHeader
{
    public static IApplicationBuilder UsePlanaffeVersion(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[InstanceVersion.Header] = InstanceVersion.Value;
                return Task.CompletedTask;
            });

            return next(context);
        });
}
