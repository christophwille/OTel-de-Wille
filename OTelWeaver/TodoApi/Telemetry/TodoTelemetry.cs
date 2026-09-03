using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TodoApi.Telemetry;

/// <summary>
/// The one hand-written telemetry class. Everything it references by name
/// (attribute keys, span name/kind, metric names/units/instrument types) comes
/// from the Weaver-generated <c>Generated/*.g.cs</c> files, so the code cannot
/// drift from the registry without a compile error.
/// </summary>
public sealed class TodoTelemetry : IDisposable
{
    public const string SourceName = "TodoApi";

    private readonly ActivitySource _source = new(SourceName);
    private readonly Meter _meter = new(SourceName);

    private readonly Counter<long> _operations;
    private readonly Histogram<double> _operationDuration;
    private readonly UpDownCounter<long> _itemsOpen;

    public TodoTelemetry()
    {
        // Instrument type, name, unit and description are all dictated by the registry.
        _operations = TodoMetrics.TodoOperations.Create(_meter);
        _operationDuration = TodoMetrics.TodoOperationDuration.Create(_meter);
        _itemsOpen = TodoMetrics.TodoItemsOpen.Create(_meter);
    }

    /// <summary>Tracks the number of items that are not done (todo.items.open).</summary>
    public void ItemsOpenChanged(long delta) => _itemsOpen.Add(delta);

    /// <summary>
    /// Wraps one store operation in a <c>todo.operation</c> span and records the
    /// <c>todo.operations</c> counter and <c>todo.operation.duration</c> histogram.
    /// </summary>
    public Operation Start(string operation) => new(this, operation);

    public void Dispose()
    {
        _source.Dispose();
        _meter.Dispose();
    }

    public readonly struct Operation : IDisposable
    {
        private readonly TodoTelemetry _telemetry;
        private readonly string _operation;
        private readonly long _startTimestamp;
        private readonly Activity? _activity;

        internal Operation(TodoTelemetry telemetry, string operation)
        {
            _telemetry = telemetry;
            _operation = operation;
            _startTimestamp = Stopwatch.GetTimestamp();
            // Generated helper: the required attribute is a parameter, so it cannot be forgotten.
            _activity = TodoSpans.TodoOperation.Start(telemetry._source, operation);
        }

        public Operation WithItem(int id) { _activity?.SetTag(TodoAttributes.TodoId, id); return this; }
        public Operation WithStatus(bool isDone) { _activity?.SetTag(TodoAttributes.TodoStatus, isDone ? TodoAttributes.TodoStatusValues.Done : TodoAttributes.TodoStatusValues.Open); return this; }
        public Operation WithCount(int count) { _activity?.SetTag(TodoAttributes.TodoCount, count); return this; }

        /// <summary>Marks the operation as failed with a low-cardinality error.type (e.g. "not_found").</summary>
        public Operation Failed(string errorType)
        {
            _activity?.SetTag(ErrorAttributes.ErrorType, errorType);
            _activity?.SetStatus(ActivityStatusCode.Error);
            return this;
        }

        public void Dispose()
        {
            var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
            var errorType = _activity?.GetTagItem(ErrorAttributes.ErrorType) as string;

            var tags = new TagList { { TodoAttributes.TodoOperation, _operation } };
            if (errorType is not null)
            {
                tags.Add(ErrorAttributes.ErrorType, errorType);
            }

            _telemetry._operations.Add(1, tags);
            _telemetry._operationDuration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>(TodoAttributes.TodoOperation, _operation));
            _activity?.Dispose();
        }
    }
}
