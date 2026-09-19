namespace Planaffe.Application.Ports;

/// <summary>
/// The three variables of ADR 0029: where logaffe answers, the token that names
/// its project, and the floor. The console is always on; logaffe is optional.
/// </summary>
public sealed record LogSettings(Uri? Endpoint, string? Token, string Level)
{
    public const string EndpointVariable = "PLANAFFE_LOG_ENDPOINT";

    public const string TokenVariable = "PLANAFFE_LOG_TOKEN";

    public const string LevelVariable = "PLANAFFE_LOG_LEVEL";

    public const string DefaultLevel = "Information";

    public static readonly string[] Levels = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"];

    /// <summary>Whether entries go to logaffe: both the endpoint and the token are set.</summary>
    public bool ShipsToLogaffe => Endpoint is not null && Token is not null;

    /// <exception cref="ArgumentException">
    /// One of the pair without the other, an endpoint that is not an absolute
    /// http(s) address, or a level that is not a supported logging level.
    /// </exception>
    public static LogSettings FromVariables(string? endpoint, string? token, string? level)
    {
        var hasEndpoint = !string.IsNullOrWhiteSpace(endpoint);
        var hasToken = !string.IsNullOrWhiteSpace(token);

        if (hasEndpoint != hasToken)
        {
            throw new ArgumentException(
                $"{EndpointVariable} and {TokenVariable} go together: set both to log into logaffe, or neither to log to the console.",
                hasEndpoint ? TokenVariable : EndpointVariable);
        }

        Uri? address = null;
        if (hasEndpoint)
        {
            if (!Uri.TryCreate(endpoint!.Trim(), UriKind.Absolute, out address) || address.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException($"{EndpointVariable} is '{endpoint}'; it has to be an absolute http or https address.", EndpointVariable);
            }
        }

        var chosen = string.IsNullOrWhiteSpace(level) ? DefaultLevel : level.Trim();
        // Accept the former level names in existing instance configurations.
        chosen = chosen.ToLowerInvariant() switch
        {
            "verbose" => "Trace",
            "fatal" => "Critical",
            _ => chosen,
        };
        var known = Levels.FirstOrDefault(l => l.Equals(chosen, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"{LevelVariable} is '{level}'; it is one of {string.Join(", ", Levels)}.", LevelVariable);

        return new LogSettings(address, hasToken ? token!.Trim() : null, known);
    }
}
