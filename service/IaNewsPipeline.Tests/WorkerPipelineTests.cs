using System.Net;
using System.Text.Json;
using IaNewsPipeline.Api.Jobs;
using IaNewsPipeline.Api.Queueing;
using IaNewsPipeline.Worker.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace IaNewsPipeline.Tests;

public sealed class WorkerPipelineTests
{
    [Fact]
    public async Task ProcessAsync_marks_job_published_and_deletes_message_on_201_publish()
    {
        var fixture = WorkerFixture.Create();
        fixture.SourceFetcher.Result = FetchResult.Success("<article>source</article>");
        fixture.Extractor.Result = ExtractionResult.Success(new ExtractedArticle(
            "Original",
            "<p>Extracted</p>",
            "Extracted excerpt"));
        fixture.RewriteClient.Result = RewriteResult.Success(new RewrittenPost(
            "Rewritten headline",
            "<p>Rewritten body</p>",
            "One-paragraph summary.",
            "gpt-4o-mini",
            new DateTimeOffset(2026, 7, 7, 15, 0, 0, TimeSpan.Zero)));

        fixture.Publisher = CreatePublisher((request, body) =>
        {
            Assert.Equal("application/json; charset=utf-8", request.Content!.Headers.ContentType!.ToString());
            Assert.True(request.Headers.Contains("X-Pipeline-Timestamp"));
            Assert.True(request.Headers.Contains("X-Pipeline-Signature"));
            var timestamp = request.Headers.GetValues("X-Pipeline-Timestamp").Single();
            var signature = request.Headers.GetValues("X-Pipeline-Signature").Single();
            var expectedSignature = new WebhookSignatureService("test-shared-secret").Compute(timestamp, body);
            Assert.Equal($"sha256={expectedSignature}", signature);

            using var json = JsonDocument.Parse(body);
            Assert.Equal(fixture.Job.JobId, json.RootElement.GetProperty("job_id").GetGuid());
            Assert.Equal(fixture.Job.SourceUrl, json.RootElement.GetProperty("source_url").GetString());
            Assert.Equal("Rewritten headline", json.RootElement.GetProperty("title").GetString());
            Assert.Equal("<p>Rewritten body</p>", json.RootElement.GetProperty("content_html").GetString());
            Assert.Equal("One-paragraph summary.", json.RootElement.GetProperty("excerpt").GetString());
            Assert.Equal("gpt-4o-mini", json.RootElement.GetProperty("meta").GetProperty("model").GetString());
            Assert.Equal("2026-07-07T15:00:00.0000000Z", json.RootElement.GetProperty("meta").GetProperty("generated_at").GetString());

            return JsonResponse(HttpStatusCode.Created, """{"post_id":123,"post_url":"http://wp.local/?p=123","duplicate":false}""");
        });

        var processor = fixture.CreateProcessor();

        await processor.ProcessAsync(new QueuedJobMessage("receipt-1", fixture.Job.JobId.ToString()), CancellationToken.None);

        Assert.Equal(JobStates.Published, fixture.Store.Jobs.Single().State);
        Assert.Equal("http://wp.local/?p=123", fixture.Store.Jobs.Single().PublishedPostUrl);
        Assert.Equal(["receipt-1"], fixture.Queue.DeletedReceiptHandles);
    }

    [Fact]
    public async Task ProcessAsync_treats_duplicate_true_as_success()
    {
        var fixture = WorkerFixture.Create();
        fixture.SourceFetcher.Result = FetchResult.Success("<article>source</article>");
        fixture.Extractor.Result = ExtractionResult.Success(new ExtractedArticle("Original", "<p>Extracted</p>", "Excerpt"));
        fixture.RewriteClient.Result = RewriteResult.Success(new RewrittenPost(
            "Rewritten headline",
            "<p>Rewritten body</p>",
            "Summary",
            "gpt-4o-mini",
            DateTimeOffset.UtcNow));
        fixture.Publisher = CreatePublisher((request, body) =>
            JsonResponse(HttpStatusCode.OK, """{"post_id":123,"post_url":"http://wp.local/?p=123","duplicate":true}"""));

        await fixture.CreateProcessor().ProcessAsync(
            new QueuedJobMessage("receipt-1", fixture.Job.JobId.ToString()),
            CancellationToken.None);

        Assert.Equal(JobStates.Published, fixture.Store.Jobs.Single().State);
        Assert.Equal(["receipt-1"], fixture.Queue.DeletedReceiptHandles);
    }

    [Fact]
    public async Task ProcessAsync_leaves_message_for_retry_when_webhook_is_temporarily_unavailable_then_succeeds()
    {
        var fixture = WorkerFixture.Create();
        fixture.SourceFetcher.Result = FetchResult.Success("<article>source</article>");
        fixture.Extractor.Result = ExtractionResult.Success(new ExtractedArticle("Original", "<p>Extracted</p>", "Excerpt"));
        fixture.RewriteClient.Result = RewriteResult.Success(new RewrittenPost(
            "Rewritten headline",
            "<p>Rewritten body</p>",
            "Summary",
            "gpt-4o-mini",
            DateTimeOffset.UtcNow));

        var attempts = 0;
        fixture.Publisher = new StubPublisher(_ =>
        {
            attempts++;
            return attempts == 1
                ? PublishResult.TransientFailure("webhook_unavailable")
                : PublishResult.Success("http://wp.local/?p=123");
        });

        var processor = fixture.CreateProcessor();
        var message = new QueuedJobMessage("receipt-1", fixture.Job.JobId.ToString());

        await processor.ProcessAsync(message, CancellationToken.None);
        Assert.Empty(fixture.Queue.DeletedReceiptHandles);
        Assert.Equal(JobStates.Publishing, fixture.Store.Jobs.Single().State);

        await processor.ProcessAsync(message, CancellationToken.None);
        Assert.Equal(["receipt-1"], fixture.Queue.DeletedReceiptHandles);
        Assert.Equal(JobStates.Published, fixture.Store.Jobs.Single().State);
    }

    [Fact]
    public async Task ProcessAsync_marks_job_failed_and_deletes_message_when_source_url_is_invalid()
    {
        var fixture = WorkerFixture.Create(new JobRecord(
            Guid.NewGuid(),
            JobStates.Queued,
            "notaurl",
            null,
            null));

        await fixture.CreateProcessor().ProcessAsync(
            new QueuedJobMessage("receipt-1", fixture.Job.JobId.ToString()),
            CancellationToken.None);

        Assert.Equal(JobStates.Failed, fixture.Store.Jobs.Single().State);
        Assert.Equal("invalid_url", fixture.Store.Jobs.Single().Error);
        Assert.Equal(["receipt-1"], fixture.Queue.DeletedReceiptHandles);
    }

    [Fact]
    public async Task ProcessAsync_marks_job_failed_and_deletes_message_when_source_returns_404()
    {
        var fixture = WorkerFixture.Create();
        fixture.SourceFetcher.Result = FetchResult.PermanentFailure("source_not_found");

        await fixture.CreateProcessor().ProcessAsync(
            new QueuedJobMessage("receipt-1", fixture.Job.JobId.ToString()),
            CancellationToken.None);

        Assert.Equal(JobStates.Failed, fixture.Store.Jobs.Single().State);
        Assert.Equal("source_not_found", fixture.Store.Jobs.Single().Error);
        Assert.Equal(["receipt-1"], fixture.Queue.DeletedReceiptHandles);
    }

    [Fact]
    public async Task ProcessAsync_marks_job_failed_and_deletes_message_when_extraction_is_not_readable()
    {
        var fixture = WorkerFixture.Create();
        fixture.SourceFetcher.Result = FetchResult.Success("<html></html>");
        fixture.Extractor.Result = ExtractionResult.PermanentFailure("source_not_article");

        await fixture.CreateProcessor().ProcessAsync(
            new QueuedJobMessage("receipt-1", fixture.Job.JobId.ToString()),
            CancellationToken.None);

        Assert.Equal(JobStates.Failed, fixture.Store.Jobs.Single().State);
        Assert.Equal("source_not_article", fixture.Store.Jobs.Single().Error);
        Assert.Equal(["receipt-1"], fixture.Queue.DeletedReceiptHandles);
    }

    [Fact]
    public async Task ProcessAsync_marks_job_failed_and_deletes_message_when_webhook_returns_401()
    {
        var fixture = WorkerFixture.Create();
        PrepareSuccessfulRewritePipeline(fixture);
        fixture.Publisher = CreatePublisher((request, body) =>
            JsonResponse(HttpStatusCode.Unauthorized, """{"error":"invalid_signature"}"""));

        await fixture.CreateProcessor().ProcessAsync(
            new QueuedJobMessage("receipt-1", fixture.Job.JobId.ToString()),
            CancellationToken.None);

        Assert.Equal(JobStates.Failed, fixture.Store.Jobs.Single().State);
        Assert.Equal("webhook_unauthorized", fixture.Store.Jobs.Single().Error);
        Assert.Equal(["receipt-1"], fixture.Queue.DeletedReceiptHandles);
    }

    [Fact]
    public async Task ProcessAsync_marks_job_failed_and_deletes_message_when_webhook_returns_422()
    {
        var fixture = WorkerFixture.Create();
        PrepareSuccessfulRewritePipeline(fixture);
        fixture.Publisher = CreatePublisher((request, body) =>
            JsonResponse((HttpStatusCode)422, """{"error":"invalid_payload"}"""));

        await fixture.CreateProcessor().ProcessAsync(
            new QueuedJobMessage("receipt-1", fixture.Job.JobId.ToString()),
            CancellationToken.None);

        Assert.Equal(JobStates.Failed, fixture.Store.Jobs.Single().State);
        Assert.Equal("webhook_invalid_payload", fixture.Store.Jobs.Single().Error);
        Assert.Equal(["receipt-1"], fixture.Queue.DeletedReceiptHandles);
    }

    private static void PrepareSuccessfulRewritePipeline(WorkerFixture fixture)
    {
        fixture.SourceFetcher.Result = FetchResult.Success("<article>source</article>");
        fixture.Extractor.Result = ExtractionResult.Success(new ExtractedArticle("Original", "<p>Extracted</p>", "Excerpt"));
        fixture.RewriteClient.Result = RewriteResult.Success(new RewrittenPost(
            "Rewritten headline",
            "<p>Rewritten body</p>",
            "Summary",
            "gpt-4o-mini",
            new DateTimeOffset(2026, 7, 7, 15, 0, 0, TimeSpan.Zero)));
    }

    private static WordPressWebhookPublisher CreatePublisher(
        Func<HttpRequestMessage, string, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler));
        return new WordPressWebhookPublisher(
            httpClient,
            new WebhookOptions("http://stub.local/wp-json/ia-pipeline/v1/posts"),
            new WebhookSignatureService("test-shared-secret"));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
    }

    private sealed class WorkerFixture
    {
        private WorkerFixture(JobRecord job)
        {
            Job = job;
            Store = new FakeJobStore(job);
        }

        public JobRecord Job { get; }
        public FakeJobStore Store { get; }
        public FakeJobQueue Queue { get; } = new();
        public StubSourceFetcher SourceFetcher { get; } = new();
        public StubExtractor Extractor { get; } = new();
        public StubRewriteClient RewriteClient { get; } = new();
        public IWebhookPublisher Publisher { get; set; } = new StubPublisher(_ => PublishResult.Success("http://wp.local/?p=123"));

        public static WorkerFixture Create(JobRecord? job = null)
        {
            return new WorkerFixture(job ?? new JobRecord(
                Guid.NewGuid(),
                JobStates.Queued,
                "https://example.com/article",
                null,
                null));
        }

        public JobMessageProcessor CreateProcessor()
        {
            return new JobMessageProcessor(
                Queue,
                Store,
                SourceFetcher,
                Extractor,
                RewriteClient,
                Publisher,
                NullLogger<JobMessageProcessor>.Instance);
        }
    }

    private sealed class FakeJobQueue : IJobQueue
    {
        public List<string> DeletedReceiptHandles { get; } = [];

        public Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<QueuedJobMessage>> ReceiveAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<QueuedJobMessage>>([]);

        public Task DeleteAsync(string receiptHandle, CancellationToken cancellationToken)
        {
            DeletedReceiptHandles.Add(receiptHandle);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJobStore : IJobStore
    {
        public FakeJobStore(JobRecord job)
        {
            Jobs.Add(job);
        }

        public List<JobRecord> Jobs { get; } = [];

        public Task<JobRecord> CreateQueuedJobAsync(Uri sourceUrl, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
            => Task.FromResult(Jobs.SingleOrDefault(job => job.JobId == jobId));

        public Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken)
        {
            Update(jobId, current => current with { State = JobStates.Processing });
            return Task.CompletedTask;
        }

        public Task MarkPublishingAsync(Guid jobId, string rewriteModel, DateTimeOffset generatedAt, CancellationToken cancellationToken)
        {
            Update(jobId, current => current with
            {
                State = JobStates.Publishing,
                RewriteModel = rewriteModel,
                GeneratedAt = generatedAt,
                Error = null,
            });
            return Task.CompletedTask;
        }

        public Task MarkPublishedAsync(Guid jobId, string postUrl, string rewriteModel, DateTimeOffset generatedAt, CancellationToken cancellationToken)
        {
            Update(jobId, current => current with
            {
                State = JobStates.Published,
                PublishedPostUrl = postUrl,
                RewriteModel = rewriteModel,
                GeneratedAt = generatedAt,
                Error = null,
            });
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken)
        {
            Update(jobId, current => current with { State = JobStates.Failed, Error = error });
            return Task.CompletedTask;
        }

        private void Update(Guid jobId, Func<JobRecord, JobRecord> update)
        {
            var index = Jobs.FindIndex(job => job.JobId == jobId);
            Jobs[index] = update(Jobs[index]);
        }
    }

    private sealed class StubSourceFetcher : ISourceFetcher
    {
        public FetchResult Result { get; set; } = FetchResult.Success("<article>default</article>");

        public Task<FetchResult> FetchAsync(Uri sourceUrl, CancellationToken cancellationToken)
            => Task.FromResult(Result);
    }

    private sealed class StubExtractor : IArticleExtractor
    {
        public ExtractionResult Result { get; set; } = ExtractionResult.Success(new ExtractedArticle("Default", "<p>Default</p>", "Default excerpt"));

        public Task<ExtractionResult> ExtractAsync(Uri sourceUrl, string html, CancellationToken cancellationToken)
            => Task.FromResult(Result);
    }

    private sealed class StubRewriteClient : IOpenAiRewriteClient
    {
        public RewriteResult Result { get; set; } = RewriteResult.Success(new RewrittenPost(
            "Default",
            "<p>Default</p>",
            "Default excerpt",
            "gpt-4o-mini",
            DateTimeOffset.UtcNow));

        public Task<RewriteResult> RewriteAsync(Uri sourceUrl, ExtractedArticle article, CancellationToken cancellationToken)
            => Task.FromResult(Result);
    }

    private sealed class StubPublisher(Func<WebhookPublishRequest, PublishResult> publish) : IWebhookPublisher
    {
        public Task<PublishResult> PublishAsync(WebhookPublishRequest request, CancellationToken cancellationToken)
            => Task.FromResult(publish(request));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, string, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return handler(request, body);
        }
    }
}
