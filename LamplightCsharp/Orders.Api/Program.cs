using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// "http://inventory" is resolved by Aspire service discovery (ServiceDefaults wires it into every HttpClient).
builder.Services.AddHttpClient("inventory", client => client.BaseAddress = new Uri("http://inventory"));

var app = builder.Build();
app.MapDefaultEndpoints();

// Names match the project name because ServiceDefaults registers AddSource/AddMeter(ApplicationName).
var source = new ActivitySource("Orders.Api");
var meter = new Meter("Orders.Api");
var ordersCreated = meter.CreateCounter<long>("orders.created");
var ordersRejected = meter.CreateCounter<long>("orders.rejected");
var orders = new ConcurrentDictionary<Guid, Order>();

app.MapPost("/orders", async (OrderRequest request, IHttpClientFactory factory) =>
{
    using var activity = source.StartActivity("create order");
    activity?.SetTag("order.sku", request.Sku);
    activity?.SetTag("order.quantity", request.Quantity);

    var client = factory.CreateClient("inventory");
    var response = await client.PostAsJsonAsync($"/inventory/{request.Sku}/reserve", new { request.Quantity });

    var status = response.IsSuccessStatusCode ? "created" : "rejected";
    var order = new Order(Guid.NewGuid(), request.Sku, request.Quantity, status);
    orders[order.OrderId] = order;

    activity?.SetTag("order.status", status);
    if (status == "created")
    {
        ordersCreated.Add(1, new KeyValuePair<string, object?>("sku", request.Sku));
        return Results.Created($"/orders/{order.OrderId}", order);
    }

    // A business rejection is not an error: the span status stays unset, the counter tells the story.
    ordersRejected.Add(1, new KeyValuePair<string, object?>("sku", request.Sku));
    return Results.Conflict(order);
});

app.MapGet("/orders/{id:guid}", (Guid id) =>
    orders.TryGetValue(id, out var order) ? Results.Ok(order) : Results.NotFound());

app.Run();

record OrderRequest(string Sku, int Quantity);
record Order(Guid OrderId, string Sku, int Quantity, string Status);
