using EasyNetQ;
using EnqE2E.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddEasyNetQ("host=localhost").UseSystemTextJson();
    })
    .Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

var bus = services.GetRequiredService<IBus>();

await bus.PubSub.SubscribeAsync<SampleMessage>(
        "my_subscription_id", msg => Console.WriteLine(msg.Text)
    );

Console.WriteLine("Listening in background. Press any key to exit.");
Console.Read();