using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Sample.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger) => _logger = logger;

        [HttpGet(Name = "GetWeatherForecast")]
        public async Task<IEnumerable<WeatherForecast>> Get(
            [FromServices] SqliteBloggingContext sqliteDb,
            [FromServices] SqlServerBloggingContext sqlServerDb)
        {
            using (var activity = DiagnosticsConfig.ActivitySource.StartActivity("WorkingWithSqlite"))
            {
                activity?.SetTag("enduser.id", this.User?.Identity?.Name);

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

                return Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                })
                .ToArray();
            }
        }

        private void SomeOtherMethod()
        {
            // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs#optional-populate-tags
            // https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/trace/semantic_conventions
            Activity.Current?.SetTag("code.function", nameof(SomeOtherMethod));
        }
    }
}