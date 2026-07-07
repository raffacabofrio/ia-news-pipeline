namespace IaNewsPipeline.Worker;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Worker scaffold started. Queue polling and processing arrive in Epic 1."
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
