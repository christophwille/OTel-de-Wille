using System.Diagnostics;

namespace OTelWithSerilog.Monitoring;

public static class DiagnosticsConfig
{
    private static string _serviceName = "notset";
    private static string _versionInfo = "notset";

    public static string ServiceName => _serviceName;
    public static string Version => _versionInfo;

    public static ActivitySource ActivitySource;


    public static void ConfigureDiagnosticsConfig(this WebApplicationBuilder builder)
    {
        var configureItems = builder.Configuration
            .GetSection("Serilog:WriteTo:0:Args:configure")
            .GetChildren();

        var otelSink = configureItems.FirstOrDefault(x =>
            string.Equals(x["Name"], "OpenTelemetry", StringComparison.OrdinalIgnoreCase));

        _serviceName = otelSink?["Args:resourceAttributes:service.name"];
        _versionInfo = otelSink?["Args:resourceAttributes:service.version"];

        ActivitySource = new ActivitySource(ServiceName);
    }
}
