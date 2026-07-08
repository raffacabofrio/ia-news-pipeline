using IaNewsPipeline.Api.Queueing;
using IaNewsPipeline.Worker.Services;

namespace IaNewsPipeline.Worker;

public sealed class Worker(
    IJobQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker pipeline started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await queue.ReceiveAsync(stoppingToken);

                if (messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in messages)
                {
                    using var scope = scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<JobMessageProcessor>();
                    await processor.ProcessAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker polling failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
