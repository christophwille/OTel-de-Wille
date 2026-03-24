using System.Diagnostics;

namespace EnqE2E.Consumer;

public static class DiagnosticsConfig
{
    public const string ServiceName = "Enq.Consumer";
    public static readonly ActivitySource ActivitySource = new ActivitySource(ServiceName);
}
