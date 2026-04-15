using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OTelMetrics.Monitoring;

namespace OTelMetrics;

public static class IServiceCollectionExtensions
{

    public static void AddTracingAndMetrics(this IServiceCollection services,
        bool configureForAspNet,
        string serviceName,
        string activitySourceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r =>
            {
                r.AddService(serviceName, serviceVersion: "1.0.0");
                r.AddAttributes(new Dictionary<string, object>
                {
                    ["host.name"] = Environment.MachineName,
                });
            })
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource(activitySourceName)
                    .SetErrorStatusOnException()
                    .AddHttpClientInstrumentation();

                // This is demo, don't do this in production
                tracerProviderBuilder.SetSampler(new AlwaysOnSampler());

                if (configureForAspNet)
                {
                    tracerProviderBuilder.AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    });
                }

                tracerProviderBuilder.AddOtlpExporter();
            })
            .WithMetrics(m =>
            {
                // https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/metrics/customizing-the-sdk/README.md
                m.AddMeter(InstrumentationSource.MeterName);
                m.AddAspNetCoreInstrumentation();

                m.AddOtlpExporter();
            })
            .WithLogging(builder =>
            {
                builder.AddOtlpExporter();
            });
    }
}
