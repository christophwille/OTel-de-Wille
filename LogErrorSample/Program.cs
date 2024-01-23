using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Resources;
using OTelPlayground;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddCommonOTelLogging(() => ResourceBuilder.CreateDefault().AddService(DiagnosticsConfig.ActivitySource.Name));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/exceptiontest", ([FromServices] ILogger<Program> logger) =>
{
    try
    {
        throw new ArgumentException("Having an argument");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Something bad happened");
    }

    return "done";
})
.WithName("ExceptionTest")
.WithOpenApi();

app.Run();