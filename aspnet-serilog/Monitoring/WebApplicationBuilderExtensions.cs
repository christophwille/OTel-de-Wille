using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OTelWithSerilog.Monitoring;

public record TracingAndMetricsConfiguration(string ServiceName, string ServiceVersion, string ActivitySourceName, string[] Sources);

public static class WebApplicationBuilderExtensions
{
    // NOTE: This is not being picked up (eg service.name, would be eg "unknown_service:OTelWithSerilog.exe")
    // Set service.name via https://github.com/serilog/serilog-sinks-opentelemetry?tab=readme-ov-file#resource-attributes
    // Thus: Azure Monitor/AppInsights must be exported also via a Serilog sink
    //public static void AddOpenTelemetryLogging(this WebApplicationBuilder builder, Func<ResourceBuilder> resourceBuilderFunc)
    //{
    //    builder.Logging.AddOpenTelemetry(options =>
    //    {
    //        options.IncludeScopes = true;
    //        options.IncludeFormattedMessage = true;
    //        options.ParseStateValues = true;

    //        options.SetResourceBuilder(resourceBuilderFunc());
    //        // options.AddOtlpExporter();
    //    });
    //}

    public static void AddTracingAndMetrics(this WebApplicationBuilder builder, Func<TracingAndMetricsConfiguration> configure,
        Action<TracerProviderBuilder>? extendTracerProviderBuilder = null)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var options = configure();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r =>
            {
                r.AddService(options.ServiceName, serviceVersion: options.ServiceVersion);
                r.AddAttributes(new Dictionary<string, object>
                {
                    ["host.name"] = Environment.MachineName,
                    ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant(),
                });
            })
            .WithTracing(tracerProviderBuilder =>
            {
                ConfigureTracerProviderBuilder(tracerProviderBuilder, options);

                if (null != extendTracerProviderBuilder) extendTracerProviderBuilder(tracerProviderBuilder);

                tracerProviderBuilder
                    .AddOtlpExporter();
            })
        .WithMetrics(m =>
        {
            m.AddAspNetCoreInstrumentation()
                .AddOtlpExporter();
        });
    }

    private static void ConfigureTracerProviderBuilder(TracerProviderBuilder tracerProviderBuilder, TracingAndMetricsConfiguration options)
    {
        tracerProviderBuilder
            .AddSource(options.ActivitySourceName)
            .SetErrorStatusOnException()
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                //options.EnrichWithIDbCommand = (activity, command) =>
                //{
                //    var stateDisplayName = $"{command.CommandType} main";
                //    activity.DisplayName = stateDisplayName;
                //    activity.SetTag("db.name", stateDisplayName);
                //};
            });

        if (options.Sources is not []) tracerProviderBuilder.AddSource(options.Sources);

#if DEBUG
        tracerProviderBuilder.SetSampler(new AlwaysOnSampler());
#endif
    }
}
