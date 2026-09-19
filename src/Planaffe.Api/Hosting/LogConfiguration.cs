using Logaffe.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planaffe.Application.Ports;

namespace Planaffe.Api.Hosting;

/// <summary>Structured console logs, with optional logaffe delivery (ADR 0029).</summary>
public static class LogConfiguration
{
    public static WebApplicationBuilder AddPlanaffeLogging(this WebApplicationBuilder builder, LogSettings settings)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        var minimum = Enum.Parse<LogLevel>(settings.Level);
        builder.Services.PostConfigure<LoggerFilterOptions>(options =>
        {
            // PLANAFFE_LOG_LEVEL is the sole floor; the framework's default
            // Logging section must not silently override it.
            options.MinLevel = minimum;
            options.Rules.Clear();
            options.Rules.Add(new LoggerFilterRule(null, "Microsoft.AspNetCore", LogLevel.Warning, null));
            options.Rules.Add(new LoggerFilterRule(null, "Microsoft.EntityFrameworkCore", LogLevel.Warning, null));
        });

        if (settings.ShipsToLogaffe)
        {
            builder.Logging.AddLogaffe(options =>
            {
                options.Installation = settings.Endpoint!;
                options.IngestToken = settings.Token!;
                options.Instance = Environment.MachineName;
                options.IncludeScopes = true;
                options.OnFailure = (message, exception) =>
                    Console.Error.WriteLine(exception is null ? message : $"{message} {exception}");
            });
        }

        return builder;
    }
}
