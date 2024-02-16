﻿using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            // options.AddConsoleExporter();
            options.AddOtlpExporter();
        });
    }

    // Sample: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/customizing-the-sdk/Program.cs
    // Azure Monitor: https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=net
    public static void AddCommonOTelMonitoring(this WebApplicationBuilder builder, string ServiceName, string ServiceVersion, string ActivitySourceName)
    {
        // https://opentelemetry.io/docs/instrumentation/net/getting-started/
        // https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.EntityFrameworkCore

        // https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/getting-started-aspnetcore/Program.cs
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
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    // .AddSqlClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        //options.EnrichWithIDbCommand = (activity, command) =>
                        //{
                        //    var stateDisplayName = $"{command.CommandType} main";
                        //    activity.DisplayName = stateDisplayName;
                        //    activity.SetTag("db.name", stateDisplayName);
                        //};
                    })
                    .AddOtlpExporter();

                if (builder.Environment.IsDevelopment())
                {
                    // eg: tracerProviderBuilder.AddConsoleExporter();
                }
            })
        .WithMetrics(m =>
        {
            m.AddAspNetCoreInstrumentation()
                .AddOtlpExporter();
            // .AddConsoleExporter(); // that creates a lot of noise
        });
    }
}
