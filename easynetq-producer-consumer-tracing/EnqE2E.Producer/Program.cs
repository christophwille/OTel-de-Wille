using EasyNetQ;
using EnqE2E.Messages;
using EnqE2E.Monitoring;
using EnqE2E.Producer;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTracingAndMetrics(
    configureForAspNet: true,
    serviceName: DiagnosticsConfig.ServiceName,
    activitySourceName: DiagnosticsConfig.ActivitySource.Name);

builder.Services.AddOpenApi();

builder.Services.AddEasyNetQ("host=localhost").UseSystemTextJson();

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

app.MapGet("/enqenque", async ([FromServices] IBus bus) =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    // Remember: No consumer running? No message published!
    await bus.PubSub.PublishAsync(new SampleMessage { Text = "Hello World" });

    return forecast;
})
.WithName("ProduceEnqMessage");

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
