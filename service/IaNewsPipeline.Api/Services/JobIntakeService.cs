using IaNewsPipeline.Api.Jobs;
using IaNewsPipeline.Api.Queueing;

namespace IaNewsPipeline.Api.Services;

public sealed class JobIntakeService(IJobStore jobStore, IJobQueue jobQueue, ILogger<JobIntakeService> logger)
{
    public async Task<JobIntakeResult> CreateJobAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        var job = await jobStore.CreateQueuedJobAsync(sourceUrl, cancellationToken);

        try
        {
            await jobQueue.EnqueueAsync(job.JobId, cancellationToken);
            return JobIntakeResult.Success(job);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Queue publish failed for job {JobId}", job.JobId);

            // We fail closed and leave the job observable instead of returning 202
            // after persistence without a queue dispatch.
            await jobStore.MarkFailedAsync(job.JobId, "enqueue_failed", cancellationToken);

            return JobIntakeResult.Failure(job, "enqueue_failed");
        }
    }
}

public sealed record JobIntakeResult(JobRecord? Job, bool Succeeded, string? FailureReason)
{
    public static JobIntakeResult Success(JobRecord job) => new(job, true, null);
    public static JobIntakeResult Failure(JobRecord job, string reason) => new(job, false, reason);
}
