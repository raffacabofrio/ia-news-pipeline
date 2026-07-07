namespace IaNewsPipeline.Api.Jobs;

public interface IJobStore
{
    Task<JobRecord> CreateQueuedJobAsync(Uri sourceUrl, CancellationToken cancellationToken);
    Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken);
}
