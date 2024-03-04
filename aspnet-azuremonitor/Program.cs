using OpenTelemetry.Resources;
using OTelPlayground;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddCommonOTelLogging(() => ResourceBuilder.CreateDefault().AddService(DiagnosticsConfig.ActivitySource.Name));
builder.AddCommonOTelMonitoring(DiagnosticsConfig.ServiceName, DiagnosticsConfig.GetVersion(), DiagnosticsConfig.ActivitySource.Name);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Call an unsuccessful HTTP API, report error
app.MapGet("/nestedhttp", async () =>
{
    using var activity = DiagnosticsConfig.ActivitySource.StartActivity("NestedHttpDemo");

    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync("http://httpstat.us/500");

    if (!response.IsSuccessStatusCode)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Use this text give more information about the error");
    }

    return "done";
})
.WithName("NestedHttp")
.WithOpenApi();

// Only some stubs of recording activity and events
app.MapGet("/generictest", (HttpContext context) =>
{
    using (var activity = DiagnosticsConfig.ActivitySource.StartActivity("SomeCustomActivity"))
    {
        activity?.AddEvent(new ActivityEvent("I have done something"));
    }
})
.WithName("GenericTest")
.WithOpenApi();

app.Run();
