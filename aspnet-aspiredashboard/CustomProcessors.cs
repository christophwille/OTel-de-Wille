using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Diagnostics;

namespace OTelPlayground
{
    // See https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/extending-the-sdk/MyEnrichingProcessor.cs
    public class CustomActivityEnricherProcessor : BaseProcessor<Activity>
    {
        private readonly ILogger<CustomActivityEnricherProcessor> _logger;

        public CustomActivityEnricherProcessor(ILogger<CustomActivityEnricherProcessor> logger)
        {
            // ILogger used for demo purposes only to show DI works for anything
            _logger = logger;
        }

        public override void OnEnd(Activity activity)
        {
            activity.SetTag("tenant.id", DiagnosticsConfig.GetSaasTenant());
        }
    }

    // https://aws.amazon.com/blogs/dotnet/developing-custom-processors-using-opentelemetry-in-net-8/
    public class CustomLogRecordEnricherProcessor : BaseProcessor<LogRecord>
    {
        public override void OnEnd(LogRecord logRecord)
        {
            logRecord.Attributes = logRecord!.Attributes!.Append(new KeyValuePair<string, object>("TenantId", DiagnosticsConfig.GetSaasTenant())).ToList();
            base.OnEnd(logRecord);
        }
    }
}
