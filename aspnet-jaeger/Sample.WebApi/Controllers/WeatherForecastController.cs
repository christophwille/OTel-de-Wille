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
        public IEnumerable<WeatherForecast> Get([FromServices] SqliteBloggingContext db)
        {
            using (var activity = DiagnosticsConfig.ActivitySource.StartActivity("WorkingWithDb"))
            {
                activity?.SetTag("enduser.id", this.User?.Identity?.Name);

                db.Add(new Blog { Url = "http://blogs.msdn.com/adonet" });
                db.SaveChanges();
                activity?.AddEvent(new ActivityEvent("Created blog in database"));

                var blog = db.Blogs
                    .OrderBy(b => b.BlogId)
                    .First();

                blog.Url = "https://devblogs.microsoft.com/dotnet";
                blog.Posts.Add(new Post { Title = "Hello World", Content = "I wrote an app using EF Core!" });
                db.SaveChanges();
                activity?.AddEvent(new ActivityEvent("Updated blog url and added a post"));
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