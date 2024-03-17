using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace OTelPlayground;

public static class ILoggingBuilderExtensions
{
    public static void AddCommonOTelLogging(this ILoggingBuilder builder, Func<ResourceBuilder> resourceBuilderFunc, bool enableAzureMonitor)
    {
        builder.ClearProviders();

        builder.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;

            options.SetResourceBuilder(resourceBuilderFunc());
            options.AddConsoleExporter();
            options.AddOtlpExporter();

            if (enableAzureMonitor)
            {
                // Do it
            }
        });
    }
}

public static class WebApplicationBuilderExtensions
{
    public static void AddCommonOTelLogging(this WebApplicationBuilder builder, Func<ResourceBuilder> resourceBuilderFunc)
    {
        // The intention here is to eg look at builder.Configuration which OTel functionality to enable
        // eg if Azure Monitor env var is set, only then enable Azure Monitor (and keep startup logging to a minimum)
        bool enableAzureMonitor = false;
        builder.Logging.AddCommonOTelLogging(resourceBuilderFunc, enableAzureMonitor);
    }
}
