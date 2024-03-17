using OpenTelemetry.Resources;
using OTelPlayground;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Func<ResourceBuilder> resourceBuilderFunc = () => ResourceBuilder.CreateDefault().AddService(DiagnosticsConfig.ActivitySource.Name);

// Add OpenTelemetry logging like usual (which is active after the builder.Build() call)
builder.AddCommonOTelLogging(resourceBuilderFunc);

// Create a logger that can be used during startup but does logging with OpenTelemetry already
var loggerFactory = LoggerFactory.Create(b =>
{
    b.AddCommonOTelLogging(resourceBuilderFunc, enableAzureMonitor: false);
});
var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Log info/warn/error to console & otlp without builder.Services.BuildServiceProvider()");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// Don't do anything OpenTelemetry'ish
app.MapGet("/noactivity", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("NoActivity")
.WithOpenApi();

app.Run();