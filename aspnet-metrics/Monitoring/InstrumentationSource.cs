using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OTelMetrics.Monitoring;

// FROM: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/examples/AspNetCore/InstrumentationSource.cs

/// <summary>
/// It is recommended to use a custom type to hold references for
/// ActivitySource and Instruments. This avoids possible type collisions
/// with other components in the DI container.
/// </summary>
public sealed class InstrumentationSource : IDisposable
{
    internal const string ActivitySourceName = "OTelMetrics.Sample";
    internal const string MeterName = "OTelMetrics.Sample";
    private readonly Meter meter;

    public InstrumentationSource()
    {
        var version = typeof(InstrumentationSource).Assembly.GetName().Version?.ToString();
        ActivitySource = new ActivitySource(ActivitySourceName, version);

        meter = new Meter(MeterName, version);
        FreezingDaysCounter = meter.CreateCounter<long>("weather.days.freezing", description: "The number of days where the temperature is below freezing");
        _ordersByCustomerCounter = meter.CreateCounter<long>("orders.count", description: "The number of orders by customer");
    }

    public ActivitySource ActivitySource { get; }

    public Counter<long> FreezingDaysCounter { get; }
    private Counter<long> _ordersByCustomerCounter { get; }

    // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#multi-dimensional-metrics
    // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/general/metrics.md#general-guidelines
    public void IncOrdersCreatedForCustomerBy(int customerId, int quantity)
    {
        _ordersByCustomerCounter.Add(quantity,
           new KeyValuePair<string, object?>("customer.id", customerId));
    }

    public void Dispose()
    {
        this.ActivitySource.Dispose();
        this.meter.Dispose();
    }
}