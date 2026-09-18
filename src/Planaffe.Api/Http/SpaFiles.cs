using Microsoft.Net.Http.Headers;

namespace Planaffe.Api.Http;

/// <summary>
/// The application document changes with a deployment and shares some of its
/// addresses with JSON endpoints. It must therefore be revalidated and kept
/// apart from another representation of the same address, while fingerprinted
/// assets keep the static-file middleware's ordinary cache behaviour.
/// </summary>
public static class SpaFiles
{
    public static StaticFileOptions Options() => new()
    {
        OnPrepareResponse = context =>
        {
            if (string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
            {
                PrepareDocument(context.Context.Response);
            }
        },
    };

    public static void PrepareDocument(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Headers.CacheControl = "no-cache";

        var varies = response.Headers.Vary.ToString().Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!varies.Contains(HeaderNames.Accept, StringComparer.OrdinalIgnoreCase))
        {
            response.Headers.AppendCommaSeparatedValues(HeaderNames.Vary, HeaderNames.Accept);
        }
    }
}
