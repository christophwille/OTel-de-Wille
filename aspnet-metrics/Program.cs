using OTelMetrics;
using OTelMetrics.Monitoring;
using System.Diagnostics;

// Dashboard: podman run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
// Test-URL: https://localhost:7217/weatherforecast

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InstrumentationSource>();

builder.Services.AddTracingAndMetrics(
            configureForAspNet: false,
            serviceName: InstrumentationSource.ActivitySourceName,
            activitySourceName: InstrumentationSource.ActivitySourceName);


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

app.MapGet("/weatherforecast", (ILogger<Program> logger, InstrumentationSource otel) =>
{
    logger.LogInformation("Entering {Endpoint} endpoint", "GetWeatherForecast");
    Activity.Current?.AddEvent(new ActivityEvent("And now for the real work"));

    otel.IncOrdersCreatedForCustomerBy(123, 1);

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    otel.FreezingDaysCounter.Add(forecast.Count(f => f.TemperatureC < 0));
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
