using EasyNetQ;
using EnqE2E.Messages;
using Microsoft.Extensions.Hosting;

namespace EnqE2E.Consumer
{
    internal class Worker : BackgroundService
    {
        private readonly IBus _bus;

        public Worker(IBus bus)
        {
            _bus = bus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _bus.PubSub.SubscribeAsync<SampleMessage>(
                    "my_subscription_id", msg =>
                    {
                        var activity = DiagnosticsConfig.ActivitySource.StartActivity("Processing message");
                        activity?.AddTag("log.text", msg.Text);

                        Console.WriteLine(msg.Text);

                        activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);
                    }
                );

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
