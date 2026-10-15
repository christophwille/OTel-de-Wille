var builder = DistributedApplication.CreateBuilder(args);

// Lamplight's embedded OTLP/HTTP receiver. Services export to the Aspire dashboard AND here (see ServiceDefaults).
const string lamplightOtlp = "http://127.0.0.1:4318";

var inventory = builder.AddProject<Projects.Inventory_Api>("inventory")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("LAMPLIGHT_OTLP_ENDPOINT", lamplightOtlp);

builder.AddProject<Projects.Orders_Api>("orders")
    .WithReference(inventory)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("LAMPLIGHT_OTLP_ENDPOINT", lamplightOtlp);

builder.Build().Run();
