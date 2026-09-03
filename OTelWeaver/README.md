# OTelWeaver – a ToDo Web API instrumented from an OpenTelemetry Weaver registry

A minimal ASP.NET Core ToDo API (.NET 10) whose custom telemetry is **defined once** in an
[OpenTelemetry Weaver](https://github.com/open-telemetry/weaver) semantic-convention
registry. Weaver then

1. **validates** the registry (`weaver registry check`),
2. **generates the C# constants and helpers** the app uses (`weaver registry generate`),
3. **generates markdown docs** for the conventions (`weaver registry generate ... markdown`), and
4. **checks the telemetry the running app actually emits** against the registry (`weaver registry live-check`).

There is no official C# example in [opentelemetry-weaver-examples](https://github.com/open-telemetry/opentelemetry-weaver-examples)
(it has Rust), so this sample fills that gap. Layout and workflow follow the official `basic` example.

- **`registry/`** – the source of truth: attributes, spans, metrics, and imports from the official OTel conventions.
- **`templates/registry/csharp/`** – Jinja2 templates + `weaver.yaml` that turn the registry into C#.
- **`TodoApi/`** – the Web API. `Telemetry/Generated/*.g.cs` is Weaver output and is committed;
  `Telemetry/TodoTelemetry.cs` is the only hand-written telemetry code.
- **`docs/`** – markdown generated with the official semantic-conventions templates.
- **`weaver.ps1`** – wrapper for the Weaver commands (the official examples use a Makefile for this).

## Prerequisites

- .NET 10 SDK
- Weaver 0.26+. On Windows:

  ```pwsh
  winget install OpenTelemetry.Weaver   # then open a new shell so `weaver` is on PATH
  weaver --version
  ```

  Alternatives: the zip/msi from the [releases page](https://github.com/open-telemetry/weaver/releases)
  (`weaver-x86_64-pc-windows-msvc.zip`), or Docker (`docker run --rm -v ${PWD}:/work otel/weaver:v0.26.1 registry check -r /work/registry`).
  Weaver downloads the OTel semantic-conventions dependency on first use, so the first run needs network access.

## Quick run

```pwsh
cd src/OTelWeaver
./weaver.ps1 check                      # validate registry/
./weaver.ps1 generate                   # regenerate TodoApi/Telemetry/Generated/*.g.cs
dotnet run --project TodoApi            # console exporter: spans + metrics printed to stdout
```

Exercise the API from a second shell (or use `TodoApi/TodoApi.http`):

```pwsh
curl -X POST http://localhost:5080/todos -H "Content-Type: application/json" -d '{"title":"Read the Weaver docs"}'
curl http://localhost:5080/todos
curl -X POST http://localhost:5080/todos/1/complete
curl http://localhost:5080/todos/999      # 404 -> error.type = not_found
```

The console shows the `todo.operation` spans with `todo.operation`, `todo.id`, `todo.status`,
`todo.count` and `error.type` tags, and the `todo.operations`, `todo.operation.duration`
and `todo.items.open` metrics, exactly as named in the registry.

## Step 1 – Define the telemetry in the registry

A registry is a directory of YAML files plus a manifest. Weaver's own docs:
[define your own telemetry schema](https://github.com/open-telemetry/weaver/blob/main/docs/define-your-own-telemetry-schema.md).

**`registry/manifest.yaml`** names the registry and declares a dependency on the official
OTel semantic conventions (a pinned zip of the `model` folder). The dependency lets us
`ref:` attributes such as `error.type` instead of redefining them.

```yaml
name: todo
schema_url: https://example.com/todo/schemas/0.1.0
dependencies:
  - schema_url: https://opentelemetry.io/schemas/1.37.0
    registry_path: https://github.com/open-telemetry/semantic-conventions/archive/refs/tags/v1.37.0.zip[model]
```

**`registry/todo-attributes.yaml`** is an `attribute_group` with every custom attribute:
`todo.id` (int), `todo.operation` (enum: create/list/get/update/complete/delete),
`todo.status` (enum: open/done), `todo.count` (int). It also `ref:`s `error.type` so the
generator emits a constant for it.

**`registry/todo-signals.yaml`** defines what the app emits and which attributes each
signal carries, with requirement levels:

```yaml
groups:
  - id: span.todo.operation          # span name = id without the `span.` prefix
    type: span
    span_kind: internal
    attributes:
      - ref: todo.operation
        requirement_level: required
      - ref: todo.id
        requirement_level:
          conditionally_required: If the operation targets a single item.
      - ref: error.type
        requirement_level:
          conditionally_required: If the operation failed.

  - id: metric.todo.operations
    type: metric
    metric_name: todo.operations
    instrument: counter
    unit: "{operation}"
    attributes:
      - ref: todo.operation
        requirement_level: required
```

(plus `todo.operation.duration` histogram in seconds and `todo.items.open` updowncounter).

**`registry/otel-imports.yaml`** imports the signals the app emits *without* our code:
the `service` and `telemetry.sdk` resource entities, the `span.http.server` span and the
`http.server.*` metrics from ASP.NET Core. Without this, `live-check` would report every
`http.request.method` or `service.name` as "does not exist in the registry".

```yaml
imports:
  entities: [service, telemetry.sdk]
  spans: [span.http.server]
  metrics: [http.server.*]
```

Validate:

```pwsh
weaver registry check -r registry
```

Weaver warns about an import pattern that matches nothing, and about deprecated syntax,
which is how the manifest ended up as `manifest.yaml` with `schema_url` (the older
`registry_manifest.yaml` / `schema_base_url` form still works but is flagged).

## Step 2 – Templates: registry → C#

`weaver registry generate` renders [MiniJinja](https://github.com/mitsuhiko/minijinja)
templates. The `-t templates` argument points at the folder that contains `registry/<target>/`,
and the positional `csharp` argument picks `templates/registry/csharp/`. Weaver loads the
`weaver.yaml` in that folder; reference: [Weaver Forge](https://github.com/open-telemetry/weaver/blob/main/crates/weaver_forge/README.md).

**`weaver.yaml`** (abridged):

```yaml
params:
  namespace: TodoApi.Telemetry          # override: -D namespace=Other.Namespace
  exclude_root_namespace: [http]        # imported http.* signals: validate, but generate no C#

text_maps:                              # lookup tables for the `map_text` filter
  csharp_instruments:
    counter: Counter<long>
    updowncounter: UpDownCounter<long>
    histogram: Histogram<double>
  csharp_activity_kinds:
    internal: ActivityKind.Internal
    server: ActivityKind.Server

templates:
  - template: attributes.cs.j2
    filter: semconv_grouped_attributes($params)   # jq: attributes grouped by root namespace
    application_mode: each                        # render once per group ...
    file_name: "{{ ctx.root_namespace | pascal_case }}Attributes.g.cs"   # ... into its own file
  - template: metrics.cs.j2
    filter: semconv_grouped_metrics($params)
    application_mode: each
    file_name: "{{ ctx.root_namespace | pascal_case }}Metrics.g.cs"
  - template: spans.cs.j2
    filter: 'semconv_signal("span"; $params) | group_by(.root_namespace) | map({root_namespace: .[0].root_namespace, spans: sort_by(.id)})'
    application_mode: each
    file_name: "{{ ctx.root_namespace | pascal_case }}Spans.g.cs"
```

Things worth knowing when writing templates:

- `filter` is a **jq** expression run over the resolved registry. `semconv_grouped_attributes`
  and `semconv_grouped_metrics` are built in; there is no grouped-spans helper, so the span
  filter groups by hand (single quotes are needed because the jq contains a colon).
- The template sees the filter output as `ctx`. Resolved attributes use `attr.name`
  (not `id`), metrics use `metric.metric_name`, spans use `span.id` and `span.span_kind`.
- Useful filters: `pascal_case_const` (`todo.id` → `TodoId`), `camel_case_const`,
  `map_text("csharp_instruments")`, `attribute_sort`, `required` (attributes whose
  requirement level is `required`), `instantiated_type` (enum → its member type),
  `comment` (renders `brief`/`note` with the `/// ` prefix from `comment_formats`), `tojson`.

**`attributes.cs.j2`** → `TodoAttributes.g.cs` / `ErrorAttributes.g.cs`: a `const string`
per attribute plus a nested `<Attr>Values` class for enums.

**`metrics.cs.j2`** → `TodoMetrics.g.cs`: per metric a nested class with `Name`, `Unit`,
`Description` and a `Create(Meter)` factory whose return type comes from the instrument via
`text_maps`. The instrument type therefore cannot drift from the registry.

**`spans.cs.j2`** → `TodoSpans.g.cs`: per span `Name`, `Kind` and a `Start(ActivitySource, ...)`
helper whose extra parameters are the span's **required** attributes, typed via `text_maps`:

```csharp
public static class TodoOperation
{
    public const string Name = "todo.operation";
    public const ActivityKind Kind = ActivityKind.Internal;

    public static Activity? Start(ActivitySource source, string todoOperation)
    {
        var activity = source.StartActivity(Name, Kind);
        activity?.SetTag(TodoAttributes.TodoOperation, todoOperation);
        return activity;
    }
}
```

## Step 3 – Generate

```pwsh
weaver registry generate -r registry -t templates csharp TodoApi/Telemetry/Generated
#                        ^registry    ^templates root ^target ^output dir
```

or `./weaver.ps1 generate`. Re-run after every registry or template change; the output is
committed so reviewers see telemetry changes as C# diffs. `./weaver.ps1 verify` regenerates
and fails if the committed files are stale (the CI check the official example does with
`git diff --exit-code`).

## Step 4 – How the app uses the generated code

`TodoApi/Telemetry/TodoTelemetry.cs` is the only hand-written telemetry code. Every name in
it comes from the generated files:

```csharp
_operations        = TodoMetrics.TodoOperations.Create(_meter);        // Counter<long>, "{operation}"
_operationDuration = TodoMetrics.TodoOperationDuration.Create(_meter); // Histogram<double>, "s"
_itemsOpen         = TodoMetrics.TodoItemsOpen.Create(_meter);         // UpDownCounter<long>, "{item}"

_activity = TodoSpans.TodoOperation.Start(source, operation);          // required attr is a parameter
_activity?.SetTag(TodoAttributes.TodoId, id);
_activity?.SetTag(TodoAttributes.TodoStatus, TodoAttributes.TodoStatusValues.Done);
_activity?.SetTag(ErrorAttributes.ErrorType, "not_found");
```

Each endpoint in `Program.cs` wraps its store call:

```csharp
todos.MapGet("/{id:int}", (int id, TodoStore store, TodoTelemetry telemetry) =>
{
    using var op = telemetry.Start(TodoAttributes.TodoOperationValues.Get).WithItem(id);
    if (store.Get(id) is not { } item)
    {
        op.Failed("not_found");          // error.type on span + counter, status = Error
        return Results.NotFound();
    }
    op.WithStatus(item.IsDone);
    return Results.Ok(item);
});
```

Disposing `op` records `todo.operations` and `todo.operation.duration` and ends the span.
Rename an attribute in the registry, regenerate, and the build breaks where it is used.

OpenTelemetry wiring (`Program.cs`): `AddOpenTelemetry()` with a `todo-api` service
resource, tracing from our `ActivitySource` plus ASP.NET Core server spans, metrics from
our `Meter` plus the `Microsoft.AspNetCore.Hosting` meter. Traces and metrics go to OTLP
when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, otherwise to the console. Logs are not exported:
the registry defines no log events, so framework startup logs would only produce findings.

## Step 5 – Live-check: does the running app honour the registry?

`weaver registry live-check` starts an OTLP/gRPC receiver, compares every span, metric
data point and resource it receives with the registry, and prints a report with
**violation / improvement / information** findings when stopped.

Shell 1:

```pwsh
./weaver.ps1 live-check
# = weaver registry live-check -r registry --otlp-grpc-port 4317 --admin-port 4320 --inactivity-timeout 60
```

Shell 2:

```pwsh
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
dotnet run --project TodoApi
```

Shell 3: run the curl commands from *Quick run*, wait ~10 s for the batch exporters, then
stop live-check with `Ctrl+C` in shell 1 or `curl -X POST http://localhost:4320/stop`.

Report excerpt from this sample (0 violations):

```
Instrumentation scope TodoApi

Span todo.operation `internal`
    todo.operation = get
        - [improvement] Attribute 'todo.operation' is not stable; stability = development.
    todo.id = 999
    error.type = not_found
        - [information] Enum attribute 'error.type' has value 'not_found' which is not documented.

Metric todo.operations `counter`, `{operation}`
    Data point 1
        - [information] Conditionally required attribute 'error.type' is not present.
        todo.operation = list

Advisories given
  - advice type:
    - conditionally_required_attribute_not_present: 11
    - not_stable: 24
    - opt_in_attribute_not_present: 16
    - undefined_enum_variant: 2

Registry coverage
  - total seen: 55.56%
```

How to read it: `not_stable` is because everything here is `stability: development`; the
`error.type` notes are expected (it is only set on failures, and `not_found` is our own
value, which `error.type` explicitly allows). `--fail-on violation` is the default, so the
process exits non-zero only for violations.

**Try a violation.** Add an undeclared tag in `TodoTelemetry.Operation.WithItem`:

```csharp
_activity?.SetTag("todo.priority", "high");
```

Rebuild, repeat the run, and the report now contains

```
    todo.priority = high
        - [violation] Attribute 'todo.priority' does not exist in the registry.
        - [information] Attribute key 'todo.priority' collides with existing namespace 'todo'
```

and live-check exits with code 1. The fix is to add `todo.priority` to
`registry/todo-attributes.yaml`, `ref:` it from the span, regenerate, and use
`TodoAttributes.TodoPriority`.

Notes learned while building this:

- Attributes and signals from the dependency are only known to live-check if they are
  imported (`registry/otel-imports.yaml`) or referenced. `--include-unreferenced` also works
  but is deprecated in favour of imports.
- The .NET OTLP metric exporter flushes every 60 s by default; the app sets 5 s so a short
  session already delivers metrics.
- `AddAspNetCoreInstrumentation()` for **metrics** also enables Kestrel, routing and
  memory-pool meters; those are not imported here, so the app adds only the hosting meter.
- Imported signals become part of the resolved registry, so without
  `exclude_root_namespace: [http]` the generator would also emit `HttpSpans.g.cs` and
  `HttpMetrics.g.cs` (which do not compile, since their attributes are not generated).

## Step 6 – Generate markdown docs

The official semantic-conventions repo ships markdown templates that work for custom
registries too:

```pwsh
weaver registry generate -r registry -t "https://github.com/open-telemetry/semantic-conventions/archive/refs/tags/v1.37.0.zip[templates]" markdown docs
```

or `./weaver.ps1 docs`. Result: `docs/attributes/todo.md` (attribute table with enum
values and stability badges) and `docs/entities/*.md` for the imported entities.

## Optional – see it in a dashboard

Any OTLP backend works as an alternative to live-check, e.g. the standalone Aspire dashboard:

```pwsh
docker run --rm -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
dotnet run --project TodoApi
```

## Not covered (next steps)

- Log events (`type: event`) in the registry and a matching template.
- Rego policies (`weaver registry check -p policies/`) and custom live-check advisors.
- `weaver registry diff --baseline-registry <old> -r registry` for breaking-change detection in CI.

## References

- Weaver: <https://github.com/open-telemetry/weaver> (usage: `docs/usage.md`, schema syntax: `schemas/semconv-syntax.md`)
- Official examples (Rust): <https://github.com/open-telemetry/opentelemetry-weaver-examples>
- Template engine and filters: <https://github.com/open-telemetry/weaver/blob/main/crates/weaver_forge/README.md>
- Go walkthrough this layout mirrors: <https://telemetrydrops.com/blog/weaver-from-zero-to-hero/>
- Blog: <https://opentelemetry.io/blog/2025/otel-weaver/>
