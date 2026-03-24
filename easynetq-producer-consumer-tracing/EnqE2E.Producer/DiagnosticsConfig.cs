using System.Diagnostics;

namespace EnqE2E.Producer;

public static class DiagnosticsConfig
{
    public const string ServiceName = "Enq.Producer";
    public static readonly ActivitySource ActivitySource = new ActivitySource(ServiceName);
}