namespace IaNewsPipeline.Api.Jobs;

public interface IJobStore
{
    Task<JobRecord> CreateQueuedJobAsync(Uri sourceUrl, CancellationToken cancellationToken);
    Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken);
    Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken);
    Task MarkPublishingAsync(
        Guid jobId,
        string rewriteModel,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken);
    Task MarkPublishedAsync(
        Guid jobId,
        string postUrl,
        string rewriteModel,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken);
}
