using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

// app.UseHttpsRedirection();

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

// Play with Logger BeginScope
app.MapGet("/loggerbeginscope", ([FromServices] ILogger<Program> logger) =>
{
    using (logger.BeginScope("Static scope string"))
    {
        logger.LogInformation("Processing...");
        logger.LogInformation("even more processing...");
    }

    using (logger.BeginScope("String with {CustomerId}", 4711))
    {
        logger.LogInformation("Processing...");
        logger.LogInformation("even more processing...");
    }

    // Scope till the end with a dictionary
    using var _ = logger.BeginScope(new Dictionary<string, object>
    {
        ["CustomerId"] = 4711
    });

    logger.LogInformation("Processing...");
    logger.LogInformation("even more processing...");

    return "done";
})
.WithName("LoggerBeginScope")
.WithOpenApi();

// Record an exception
app.MapGet("/exceptiontest", ([FromServices] ILogger<Program> logger) =>
{
    try
    {
        throw new ArgumentException("Having an argument");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Something bad happened");

        // Note: You need "using OpenTelemetry.Trace" (OpenTelemetry package) because it is an extension method
        // https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/reporting-exceptions/README.md
        Activity.Current?.RecordException(ex);
    }

    return "done";
})
.WithName("ExceptionTest")
.WithOpenApi();

// Report an error
app.MapGet("/errortest", () =>
{
    using var activity = DiagnosticsConfig.ActivitySource.StartActivity("StatusDemo");

    // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs#optional-add-status
    //activity?.SetTag("otel.status_code", "ERROR");
    //activity?.SetTag("otel.status_description", "Use this text give more information about the error");

    // Better way (.NET way)
    activity?.SetStatus(ActivityStatusCode.Error, "Use this text give more information about the error");

    return "done";
})
.WithName("ErrorTest")
.WithOpenApi();

// Call an unsuccessful HTTP API, report error
app.MapGet("/nestedhttp", async () =>
{
    using var activity = DiagnosticsConfig.ActivitySource.StartActivity("NestedHttpDemo");

    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync("http://httpstat.us/500");

    if (!response.IsSuccessStatusCode)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Use this text give more information about the error");
    }

    return "done";
})
.WithName("NestedHttp")
.WithOpenApi();

// Same as NestedHttp, report error and then however throw (and catch) exception
app.MapGet("/nestedhttpwithex", async () =>
{
    using var activity = DiagnosticsConfig.ActivitySource.StartActivity("NestedHttpDemo");

    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync("http://httpstat.us/500");

    try
    {
        if (!response.IsSuccessStatusCode)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Use this text give more information about the error");

            throw new HttpRequestException("Something went wrong with the request", null, System.Net.HttpStatusCode.BadRequest);
        }
    }
    catch (Exception ex)
    {
        // do nothing
    }

    return "done";
})
.WithName("NestedHttpWithEx")
.WithOpenApi();

// Couple of nested actvities with databases
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

        // TagWith basics: https://learn.microsoft.com/en-us/ef/core/querying/tags (NOTE: That writes a comment to the SQL query sent to SQL Server, not OTel)
        // TagWithSource extension: https://itnext.io/practical-query-tagging-in-ef-core-ad0b38fa3436
        var blog = await sqlServerDb.Blogs
            .OrderBy(b => b.BlogId)
            .TagWith("Getting published blog posts")
            .TagWithCallSite()
            .FirstAsync();

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