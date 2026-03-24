using EasyNetQ;
using EnqE2E.Consumer;
using EnqE2E.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddTracingAndMetrics(
            configureForAspNet: false,
            serviceName: DiagnosticsConfig.ServiceName,
            activitySourceName: DiagnosticsConfig.ActivitySource.Name);

builder.Services.AddEasyNetQ("host=localhost").UseSystemTextJson();

var host = builder.Build();
host.Run();