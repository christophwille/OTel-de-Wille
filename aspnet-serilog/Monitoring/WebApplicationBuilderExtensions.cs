using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OTelWithSerilog.Monitoring;

public record TracingAndMetricsConfiguration(string ServiceName, string ServiceVersion, string ActivitySourceName, string[] Sources);

public static class WebApplicationBuilderExtensions
{
    //NOTE: This is not being picked up (eg service.name, would be eg "unknown_service:OTelWithSerilog.exe")
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

    private static string? SettingsGetAzureMonitorConnectionString(IHostApplicationBuilder builder) => builder.Configuration[Constants.AzureMonitorConnectionStringAppSetting];
    private static string? EnvGetAzureMonitorConnectionString(IHostApplicationBuilder builder) => builder.Configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING");

    private static bool SendToAzureMonitor(IHostApplicationBuilder builder)
    {
        return !string.IsNullOrWhiteSpace(SettingsGetAzureMonitorConnectionString(builder)) ||
            !string.IsNullOrWhiteSpace(EnvGetAzureMonitorConnectionString(builder));
    }

    private static string GetAzureMonitorConnectionString(IHostApplicationBuilder builder)
    {
        // Although https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-configuration?tabs=aspnetcore#connection-string 
        // says both are ok to set, in practice, using the appsetting connection string throws an exception
        string? connectionString = SettingsGetAzureMonitorConnectionString(builder);
        if (!string.IsNullOrWhiteSpace(connectionString)) return connectionString!;

        connectionString = EnvGetAzureMonitorConnectionString(builder);
        if (!string.IsNullOrWhiteSpace(connectionString)) return connectionString!;

        throw new InvalidOperationException("Azure Monitor Connection String is not set in configuration or environment variables.");
    }

    private static bool EnableOpenTelemetry(IHostApplicationBuilder builder)
    {
        string? disableOTel = builder.Configuration[Constants.DisableOpenTelemetryAppSetting];
        return string.IsNullOrWhiteSpace(disableOTel);
    }

    public static void AddTracingAndMetricsForWeb(this WebApplicationBuilder builder,
        Func<TracingAndMetricsConfiguration> configure,
        ILogger logger,
        Action<TracerProviderBuilder>? extendTracerProviderBuilder = null)
    {
        builder.AddTracingAndMetrics(configureForAspNet: true, configure, logger, extendTracerProviderBuilder);
    }

    public static void AddTracingAndMetricsForGeneric(this WebApplicationBuilder builder,
        Func<TracingAndMetricsConfiguration> configure,
        ILogger logger,
        Action<TracerProviderBuilder>? extendTracerProviderBuilder = null)
    {
        builder.AddTracingAndMetrics(configureForAspNet: false, configure, logger, extendTracerProviderBuilder);
    }

    private static void AddTracingAndMetrics(this WebApplicationBuilder builder,
        bool configureForAspNet,
        Func<TracingAndMetricsConfiguration> configure,
        ILogger logger,
        Action<TracerProviderBuilder>? extendTracerProviderBuilder = null)
    {
        if (!EnableOpenTelemetry(builder))
        {
            logger.LogWarning("OpenTelemetry is disabled via configuration. No telemetry will be collected or exported.");
            return;
        }

        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var options = configure();

        bool sendToAzureMonitor = SendToAzureMonitor(builder);

        if (configureForAspNet && sendToAzureMonitor)
        {
            logger.LogInformation("OpenTelemetry will be configured to export to Azure Monitor");

            // https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore#enable-azure-monitor-opentelemetry-for-net-nodejs-python-and-java-applications 
            // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/monitor/Azure.Monitor.OpenTelemetry.AspNetCore/README.md

            // This includes: ***Logging***, Tracing, Metrics, Live Metrics, Resource Detectors
            builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
            {
                options.ConnectionString = GetAzureMonitorConnectionString(builder);
                options.EnableLiveMetrics = true;
                // options.SamplingRatio = 0.5F;
            });

            // Adding Custom Resource
            // Adding Custom ActivitySource to Traces
            // Adding Additional Instrumentation
            // Adding Another Exporter
            builder.Services.ConfigureOpenTelemetryTracerProvider((tracerProviderBuilder) =>
            {
                tracerProviderBuilder.ConfigureResource(r => ConfigureResourceBuilder(builder, r, options));
                ConfigureTracerProviderBuilder(tracerProviderBuilder, options, extendTracerProviderBuilder);
            });

            builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(ConfigureAspNetCoreInstrumentationOptions);
            // builder.Services.ConfigureOpenTelemetryMeterProvider((builder) => builder.AddConsoleExporter());
        }
        else
        {
            logger.LogInformation("OpenTelemetry will be configured to export via standard OTLP protocol");

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => ConfigureResourceBuilder(builder, r, options))
                .WithTracing(tracerProviderBuilder =>
                {
                    ConfigureTracerProviderBuilder(tracerProviderBuilder, options, extendTracerProviderBuilder);

                    if (configureForAspNet)
                    {
                        tracerProviderBuilder.AddAspNetCoreInstrumentation(options => ConfigureAspNetCoreInstrumentationOptions(options));
                    }

                    if (builder.Environment.IsDevelopment())
                    {
                        // eg: tracerProviderBuilder.AddConsoleExporter(); // Warning: creates a lot of noise
                    }

                    if (sendToAzureMonitor)
                    {
                        tracerProviderBuilder.AddAzureMonitorTraceExporter(options => options.ConnectionString = GetAzureMonitorConnectionString(builder));
                    }
                    else
                    {
                        tracerProviderBuilder.AddOtlpExporter();
                    }
                })
                .WithMetrics(m =>
                {
                    m.AddAspNetCoreInstrumentation();

                    if (sendToAzureMonitor)
                    {
                        m.AddAzureMonitorMetricExporter(options => options.ConnectionString = GetAzureMonitorConnectionString(builder));
                    }
                    else
                    {
                        m.AddOtlpExporter();
                    }
                });
        }
    }

    private static void ConfigureResourceBuilder(WebApplicationBuilder builder, ResourceBuilder r, TracingAndMetricsConfiguration options)
    {
        r.AddService(options.ServiceName, serviceVersion: options.ServiceVersion);
        r.AddAttributes(new Dictionary<string, object>
        {
            ["host.name"] = Environment.MachineName,
            ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant(),
        });
    }

    private static void ConfigureTracerProviderBuilder(TracerProviderBuilder tracerProviderBuilder, TracingAndMetricsConfiguration options,
        Action<TracerProviderBuilder>? extendTracerProviderBuilder)
    {
        tracerProviderBuilder
            .AddSource(options.ActivitySourceName)
            .SetErrorStatusOnException()
            // builder.AddProcessor<YourProcessorHere>();
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
        if (null != extendTracerProviderBuilder) extendTracerProviderBuilder(tracerProviderBuilder);

#if DEBUG
        tracerProviderBuilder.SetSampler(new AlwaysOnSampler());
#endif
    }

    private static void ConfigureAspNetCoreInstrumentationOptions(AspNetCoreTraceInstrumentationOptions options)
    {
        options.RecordException = true;

        var hcPath = new PathString("/api/health");
        options.Filter = (httpContext) =>
        {
            if (httpContext.Request.Path.Equals(hcPath)) return false;

            return true;
        };
    }
}
