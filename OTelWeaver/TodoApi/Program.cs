using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TodoApi.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TodoTelemetry>();
builder.Services.AddSingleton<TodoStore>();

// --- OpenTelemetry wiring -------------------------------------------------
// Our custom source/meter share the name TodoTelemetry.SourceName; ASP.NET Core
// adds the incoming HTTP server spans and the http.server.* metrics.
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "todo-api", serviceVersion: "0.1.0"))
    .WithTracing(t => t
        .AddSource(TodoTelemetry.SourceName)
        .AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddMeter(TodoTelemetry.SourceName)
        // Only the hosting meter (http.server.request.duration, http.server.active_requests).
        // AddAspNetCoreInstrumentation() would also enable Kestrel/routing/memory-pool
        // meters, which we do not import into the registry (see registry/otel-imports.yaml).
        .AddMeter("Microsoft.AspNetCore.Hosting"));

// Traces and metrics go to OTLP when an endpoint is configured (e.g.
// `weaver registry live-check` or the Aspire dashboard on localhost:4317),
// to the console otherwise so a plain `dotnet run` already shows them.
// Logs are deliberately not exported: this sample's registry defines no log
// events, so the framework's startup logs would only show up as findings.
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    otel.WithTracing(t => t.AddOtlpExporter())
        .WithMetrics(m => m.AddOtlpExporter((_, reader) => reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000));
}
else
{
    otel.WithTracing(t => t.AddConsoleExporter())
        .WithMetrics(m => m.AddConsoleExporter((_, reader) => reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000));
}

var app = builder.Build();

// --- ToDo endpoints -------------------------------------------------------
// Every handler wraps its store access in `telemetry.Start(<operation>)`, which
// emits the todo.operation span and the todo.* metrics defined in the registry.
var todos = app.MapGroup("/todos");

todos.MapGet("/", (TodoStore store, TodoTelemetry telemetry) =>
{
    using var op = telemetry.Start(TodoAttributes.TodoOperationValues.List);
    var items = store.List();
    op.WithCount(items.Count);
    return Results.Ok(items);
});

todos.MapGet("/{id:int}", (int id, TodoStore store, TodoTelemetry telemetry) =>
{
    using var op = telemetry.Start(TodoAttributes.TodoOperationValues.Get).WithItem(id);
    if (store.Get(id) is not { } item)
    {
        op.Failed("not_found");
        return Results.NotFound();
    }
    op.WithStatus(item.IsDone);
    return Results.Ok(item);
});

todos.MapPost("/", (CreateTodoRequest request, TodoStore store, TodoTelemetry telemetry) =>
{
    using var op = telemetry.Start(TodoAttributes.TodoOperationValues.Create);
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        op.Failed("validation");
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Title is required."] });
    }
    var item = store.Create(request.Title);
    op.WithItem(item.Id).WithStatus(item.IsDone);
    telemetry.ItemsOpenChanged(+1);
    return Results.Created($"/todos/{item.Id}", item);
});

todos.MapPut("/{id:int}", (int id, UpdateTodoRequest request, TodoStore store, TodoTelemetry telemetry) =>
{
    using var op = telemetry.Start(TodoAttributes.TodoOperationValues.Update).WithItem(id);
    if (store.Get(id) is not { } existing)
    {
        op.Failed("not_found");
        return Results.NotFound();
    }
    var updated = store.Update(existing with { Title = request.Title ?? existing.Title, IsDone = request.IsDone ?? existing.IsDone });
    op.WithStatus(updated.IsDone);
    if (existing.IsDone != updated.IsDone)
    {
        telemetry.ItemsOpenChanged(updated.IsDone ? -1 : +1);
    }
    return Results.Ok(updated);
});

todos.MapPost("/{id:int}/complete", (int id, TodoStore store, TodoTelemetry telemetry) =>
{
    using var op = telemetry.Start(TodoAttributes.TodoOperationValues.Complete).WithItem(id);
    if (store.Get(id) is not { } existing)
    {
        op.Failed("not_found");
        return Results.NotFound();
    }
    if (!existing.IsDone)
    {
        store.Update(existing with { IsDone = true });
        telemetry.ItemsOpenChanged(-1);
    }
    op.WithStatus(isDone: true);
    return Results.NoContent();
});

todos.MapDelete("/{id:int}", (int id, TodoStore store, TodoTelemetry telemetry) =>
{
    using var op = telemetry.Start(TodoAttributes.TodoOperationValues.Delete).WithItem(id);
    if (store.Delete(id) is not { } removed)
    {
        op.Failed("not_found");
        return Results.NotFound();
    }
    if (!removed.IsDone)
    {
        telemetry.ItemsOpenChanged(-1);
    }
    return Results.NoContent();
});

app.Run();

// --- Model + in-memory store ---------------------------------------------
record Todo(int Id, string Title, bool IsDone, DateTimeOffset CreatedAt);
record CreateTodoRequest(string Title);
record UpdateTodoRequest(string? Title, bool? IsDone);

sealed class TodoStore
{
    private readonly ConcurrentDictionary<int, Todo> _items = new();
    private int _nextId;

    public List<Todo> List() => _items.Values.OrderBy(t => t.Id).ToList();
    public Todo? Get(int id) => _items.GetValueOrDefault(id);
    public Todo Create(string title)
    {
        var item = new Todo(Interlocked.Increment(ref _nextId), title.Trim(), false, DateTimeOffset.UtcNow);
        _items[item.Id] = item;
        return item;
    }
    public Todo Update(Todo item) { _items[item.Id] = item; return item; }
    public Todo? Delete(int id) => _items.TryRemove(id, out var removed) ? removed : null;
}
