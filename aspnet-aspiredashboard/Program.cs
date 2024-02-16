using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Resources;
using OTelPlayground;
using System.Diagnostics;

using var db = new SqliteBloggingContext();
await db.Database.EnsureDeletedAsync();
await db.Database.EnsureCreatedAsync();

using var sqlDb = new SqlServerBloggingContext();
await sqlDb.Database.EnsureDeletedAsync();
await sqlDb.Database.EnsureCreatedAsync();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<SqliteBloggingContext>();
builder.Services.AddDbContext<SqlServerBloggingContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddCommonOTelLogging(() => ResourceBuilder.CreateDefault().AddService(DiagnosticsConfig.ActivitySource.Name));
builder.AddCommonOTelMonitoring(DiagnosticsConfig.ServiceName, DiagnosticsConfig.GetVersion(), DiagnosticsConfig.ActivitySource.Name);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
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
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapGet("/exceptiontest", ([FromServices] ILogger<Program> logger) =>
{
    try
    {
        throw new ArgumentException("Having an argument");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Something bad happened");
    }

    return "done";
})
.WithName("ExceptionTest")
.WithOpenApi();

app.MapGet("/dbtest", async ([FromServices] SqliteBloggingContext sqliteDb, [FromServices] SqlServerBloggingContext sqlServerDb, HttpContext context) =>
{
    using (var activity = DiagnosticsConfig.ActivitySource.StartActivity("WorkingWithSqlite"))
    {
        activity?.SetTag("enduser.id", context.User?.Identity?.Name);

        sqliteDb.Add(new Blog { Url = "http://blogs.msdn.com/adonet" });
        sqliteDb.SaveChanges();
        activity?.AddEvent(new ActivityEvent("Created blog in database"));

        var blog = sqliteDb.Blogs
            .OrderBy(b => b.BlogId)
            .First();

        blog.Url = "https://devblogs.microsoft.com/dotnet";
        blog.Posts.Add(new Post { Title = "Hello World", Content = "I wrote an app using EF Core!" });
        sqliteDb.SaveChanges();
        activity?.AddEvent(new ActivityEvent("Updated blog url and added a post"));
    }

    using (var activity = DiagnosticsConfig.ActivitySource.StartActivity("WorkingWithSqlServer"))
    {
        sqlServerDb.Add(new Blog { Url = "http://blogs.msdn.com/adonet" });
        await sqlServerDb.SaveChangesAsync();

        var blog = sqlServerDb.Blogs
            .OrderBy(b => b.BlogId)
            .First();

        blog.Url = "https://devblogs.microsoft.com/dotnet";
        blog.Posts.Add(new Post { Title = "Hello World", Content = "I wrote an app using EF Core!" });
        await sqlServerDb.SaveChangesAsync();
    }

    using (var activity = DiagnosticsConfig.ActivitySource.StartActivity("BuildWeatherForecastResult"))
    {
        SomeOtherMethod();

        return Enumerable.Range(1, 5).Select(index => new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]))
        .ToArray();
    }
})
.WithName("DbTest")
.WithOpenApi();


app.Run();

static void SomeOtherMethod()
{
    // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs#optional-populate-tags
    // https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/trace/semantic_conventions
    Activity.Current?.SetTag("code.function", nameof(SomeOtherMethod));
}