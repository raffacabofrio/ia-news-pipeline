using IaNewsPipeline.Api.Jobs;
using IaNewsPipeline.Api.Queueing;

namespace IaNewsPipeline.Worker.Services;

public sealed class JobMessageProcessor(
    IJobQueue queue,
    IJobStore jobStore,
    ISourceFetcher sourceFetcher,
    IArticleExtractor extractor,
    IOpenAiRewriteClient rewriteClient,
    IWebhookPublisher publisher,
    ILogger<JobMessageProcessor> logger)
{
    public async Task ProcessAsync(QueuedJobMessage message, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.Body, out var jobId))
        {
            logger.LogWarning("Discarding queue message with invalid job id payload");
            await queue.DeleteAsync(message.ReceiptHandle, cancellationToken);
            return;
        }

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["job_id"] = jobId,
        });

        logger.LogInformation("Received queued job");

        var job = await jobStore.GetJobAsync(jobId, cancellationToken);

        if (job is null)
        {
            logger.LogWarning("Job row not found for queued message");
            await queue.DeleteAsync(message.ReceiptHandle, cancellationToken);
            return;
        }

        if (!Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var sourceUrl))
        {
            logger.LogWarning("Job source URL is invalid");
            await FailAndDeleteAsync(jobId, message.ReceiptHandle, "invalid_url", cancellationToken);
            return;
        }

        await jobStore.MarkProcessingAsync(jobId, cancellationToken);
        logger.LogInformation("Marked job as processing");

        var fetched = await sourceFetcher.FetchAsync(sourceUrl, cancellationToken);

        if (!fetched.IsSuccess)
        {
            await HandleFailureAsync(jobId, message.ReceiptHandle, fetched.IsTransient, fetched.FailureReason!, cancellationToken);
            return;
        }

        logger.LogInformation("Fetched source article");

        var extracted = await extractor.ExtractAsync(sourceUrl, fetched.Html!, cancellationToken);

        if (!extracted.IsSuccess)
        {
            await FailAndDeleteAsync(jobId, message.ReceiptHandle, extracted.FailureReason!, cancellationToken);
            return;
        }

        logger.LogInformation("Extracted article content");

        var rewritten = await rewriteClient.RewriteAsync(sourceUrl, extracted.Article!, cancellationToken);

        if (!rewritten.IsSuccess)
        {
            await HandleFailureAsync(jobId, message.ReceiptHandle, rewritten.IsTransient, rewritten.FailureReason!, cancellationToken);
            return;
        }

        await jobStore.MarkPublishingAsync(
            jobId,
            rewritten.Post!.Model,
            rewritten.Post.GeneratedAt,
            cancellationToken);
        logger.LogInformation("Marked job as publishing");

        var publishResult = await publisher.PublishAsync(new WebhookPublishRequest(
            jobId,
            sourceUrl.ToString(),
            rewritten.Post.Title,
            rewritten.Post.ContentHtml,
            rewritten.Post.Excerpt,
            rewritten.Post.Model,
            rewritten.Post.GeneratedAt), cancellationToken);

        if (!publishResult.IsSuccess)
        {
            await HandleFailureAsync(jobId, message.ReceiptHandle, publishResult.IsTransient, publishResult.FailureReason!, cancellationToken);
            return;
        }

        await jobStore.MarkPublishedAsync(
            jobId,
            publishResult.PostUrl!,
            rewritten.Post.Model,
            rewritten.Post.GeneratedAt,
            cancellationToken);
        await queue.DeleteAsync(message.ReceiptHandle, cancellationToken);
        logger.LogInformation("Published article successfully");
    }

    private async Task HandleFailureAsync(
        Guid jobId,
        string receiptHandle,
        bool isTransient,
        string reason,
        CancellationToken cancellationToken)
    {
        if (isTransient)
        {
            logger.LogWarning("Transient worker failure: {Reason}", reason);
            return;
        }

        await FailAndDeleteAsync(jobId, receiptHandle, reason, cancellationToken);
    }

    private async Task FailAndDeleteAsync(
        Guid jobId,
        string receiptHandle,
        string reason,
        CancellationToken cancellationToken)
    {
        await jobStore.MarkFailedAsync(jobId, reason, cancellationToken);
        await queue.DeleteAsync(receiptHandle, cancellationToken);
        logger.LogWarning("Marked job as failed: {Reason}", reason);
    }
}
