using OpenTelemetry.Resources;
using OTelWithSerilog.Monitoring;
using Serilog;
using System.Diagnostics;

// Test-URL: https://localhost:7217/weatherforecast

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureDiagnosticsConfig();

// https://www.nuget.org/packages/Serilog.Sinks.OpenTelemetry/#readme-body-tab
// The following Serilog:WriteTo in appsettings.json is sufficient to make logs show up in Aspire Dashboard
//{
//    "Name": "OpenTelemetry"
//}
builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration.ReadFrom
            .Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());


builder.AddTracingAndMetrics(() => new(DiagnosticsConfig.ServiceName, DiagnosticsConfig.Version, DiagnosticsConfig.ActivitySource.Name, []));


builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (ILogger<Program> logger) =>
{
    logger.LogInformation("Entering {Endpoint} endpoint", "GetWeatherForecast");

    try
    {
        throw new ArgumentException("Having an argument");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Something bad happened");
        Activity.Current?.AddException(ex);
    }

    Activity.Current?.AddEvent(new ActivityEvent("And now for the real work"));

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
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
