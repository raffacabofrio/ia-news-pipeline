namespace IaNewsPipeline.Api.Queueing;

public interface IJobQueue
{
    Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueuedJobMessage>> ReceiveAsync(CancellationToken cancellationToken);
    Task DeleteAsync(string receiptHandle, CancellationToken cancellationToken);
}
