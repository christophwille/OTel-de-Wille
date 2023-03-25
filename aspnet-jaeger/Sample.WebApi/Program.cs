using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// https://opentelemetry.io/docs/instrumentation/net/getting-started/
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder
            .AddSource(DiagnosticsConfig.ActivitySource.Name)
            .ConfigureResource(r => r.AddService(DiagnosticsConfig.ServiceName))
            .AddAspNetCoreInstrumentation()
            .AddJaegerExporter());
//.WithMetrics(metricsProviderBuilder =>
//   metricsProviderBuilder
//       .ConfigureResource(resource => resource
//           .AddService(DiagnosticsConfig.ServiceName))
//       .AddAspNetCoreInstrumentation()
//       .AddOtlpExporter());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public static class DiagnosticsConfig
{
    public const string ServiceName = "Bad Pun Service";
    public static ActivitySource ActivitySource = new ActivitySource(ServiceName);
}