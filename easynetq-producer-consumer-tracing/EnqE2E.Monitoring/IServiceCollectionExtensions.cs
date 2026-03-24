using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EnqE2E.Monitoring
{
    public static class IServiceCollectionExtensions
    {

        public static void AddTracingAndMetrics(this IServiceCollection services,
            bool configureForAspNet,
            string serviceName,
            string activitySourceName)
        {
            // https://www.nuget.org/packages/RabbitMQ.Client.OpenTelemetry/#readme-body-tab
            var compositeTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                new BaggagePropagator()
            });
            Sdk.SetDefaultTextMapPropagator(compositeTextMapPropagator);

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
                        .AddHttpClientInstrumentation()
                        .AddRabbitMQInstrumentation();

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
                    m.AddOtlpExporter();
                });
        }
    }
}
