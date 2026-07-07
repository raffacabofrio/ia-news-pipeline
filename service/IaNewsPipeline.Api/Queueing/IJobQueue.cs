namespace IaNewsPipeline.Api.Queueing;

public interface IJobQueue
{
    Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken);
}
