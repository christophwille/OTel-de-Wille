using OpenTelemetry;
using System.Diagnostics;

namespace OTelPlayground
{
    // See https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/extending-the-sdk/MyEnrichingProcessor.cs
    public class TenantInformationEnricherProcessor : BaseProcessor<Activity>
    {
        private readonly ILogger<TenantInformationEnricherProcessor> _logger;

        public TenantInformationEnricherProcessor(ILogger<TenantInformationEnricherProcessor> logger)
        {
            // ILogger used for demo purposes only to show DI works for anything
            _logger = logger;
        }

        public override void OnEnd(Activity activity)
        {
            activity.SetTag("tenant.id", "4711");
        }
    }
}
