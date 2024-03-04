using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Monitor.OpenTelemetry.LiveMetrics;

namespace OTelPlayground;

public static class WebApplicationBuilderExtensions
{
    public static void AddCommonOTelLogging(this WebApplicationBuilder builder, Func<ResourceBuilder> resourceBuilderFunc)
    {
        builder.Logging.ClearProviders();

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;

            options.SetResourceBuilder(resourceBuilderFunc());
            options.AddAzureMonitorLogExporter(); // Azure Monitor
        });
    }

    public static void AddCommonOTelMonitoring(this WebApplicationBuilder builder, string ServiceName, string ServiceVersion, string ActivitySourceName)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r =>
            {
                r.AddService(ServiceName, serviceVersion: ServiceVersion);
                r.AddAttributes(new Dictionary<string, object>
                {
                    ["host.name"] = Environment.MachineName,
                    ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant(),
                });
            })
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource(ActivitySourceName)
                    .SetErrorStatusOnException()
                    .AddLiveMetrics() // Azure Monitor
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddAzureMonitorTraceExporter(); // Azure Monitor
            })
        .WithMetrics(m =>
        {
            m.AddAspNetCoreInstrumentation()
                .AddAzureMonitorMetricExporter(); // Azure Monitor
        });
    }
}
