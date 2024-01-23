using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Azure.Monitor.OpenTelemetry.Exporter;

namespace OTelPlayground;

public static class WebApplicationBuilderExtensions
{
    // https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/logs/getting-started-aspnetcore/Program.cs
    public static void AddCommonOTelLogging(this WebApplicationBuilder builder, Func<ResourceBuilder> resourceBuilderFunc)
    {
        builder.Logging.ClearProviders();

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;

            options.SetResourceBuilder(resourceBuilderFunc());
            options.AddConsoleExporter();
            options.AddOtlpExporter();

            // options.AddAzureMonitorLogExporter();
        });
    }
}
