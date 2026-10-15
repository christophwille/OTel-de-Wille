using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();

// Name matches the project name because ServiceDefaults registers AddSource(ApplicationName).
var source = new ActivitySource("Inventory.Api");
// ponytail: global lock + in-memory dictionary; a real service would use a database
var stock = new Dictionary<string, int> { ["WIDGET-1"] = 100, ["GADGET-7"] = 0 };

app.MapPost("/inventory/{sku}/reserve", (string sku, ReserveRequest request) =>
{
    using var activity = source.StartActivity("reserve stock");
    activity?.SetTag("inventory.sku", sku);

    lock (stock)
    {
        var available = stock.GetValueOrDefault(sku);
        if (available < request.Quantity)
        {
            // A business rejection is not an error: the span status stays unset, the attribute tells the story.
            activity?.SetTag("inventory.result", "insufficient");
            return Results.Conflict(new ReserveResponse(sku, available));
        }

        stock[sku] = available - request.Quantity;
        activity?.SetTag("inventory.result", "reserved");
        return Results.Ok(new ReserveResponse(sku, stock[sku]));
    }
});

app.Run();

record ReserveRequest(int Quantity);
record ReserveResponse(string Sku, int Remaining);
