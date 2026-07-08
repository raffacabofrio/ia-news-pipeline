using System.Net;
using IaNewsPipeline.Tests.TestSupport;
using IaNewsPipeline.Worker.Services;

namespace IaNewsPipeline.Tests;

/// <summary>
/// AC4: transient-vs-permanent failure classification is unit-tested here for each documented case from
/// 1-2-worker-pipeline.md's Implementation guardrails: transient (timeout, connection reset, 5xx, stub
/// unavailable) vs permanent (source 404, other 4xx, webhook 401, webhook 422). Each decision point is
/// exercised directly against its owning class -- <see cref="HttpSourceFetcher"/> for source-fetch
/// classification and <see cref="WordPressWebhookPublisher"/> for webhook-response classification -- using
/// an in-memory fake handler, never a real network call.
///
/// The remaining two documented cases in AC4 are covered elsewhere and intentionally not duplicated here:
///   - non-article/empty extraction -> ArticleExtractionTests.cs (SmartReaderArticleExtractor)
///   - invalid URL reaching the worker unexpectedly -> WorkerPipelineTests.cs (S1.2), because that decision
///     is a one-line Uri.TryCreate guard inside JobMessageProcessor.ProcessAsync, not a standalone seam.
/// </summary>
public sealed class FailureClassificationTests
{
    private static readonly Uri SourceUrl = new("https://example.com/article");

    private static HttpSourceFetcher CreateFetcher(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(new HttpClient(new StubHttpMessageHandler(handler)));

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "source_not_found")]
    [InlineData((HttpStatusCode)403, "source_http_403")]
    [InlineData((HttpStatusCode)410, "source_http_410")]
    public async Task FetchAsync_classifies_client_error_statuses_as_permanent(HttpStatusCode statusCode, string expectedReason)
    {
        var fetcher = CreateFetcher((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(string.Empty) }));

        var result = await fetcher.FetchAsync(SourceUrl, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal(expectedReason, result.FailureReason);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "source_http_500")]
    [InlineData(HttpStatusCode.BadGateway, "source_http_502")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "source_http_503")]
    public async Task FetchAsync_classifies_server_error_statuses_as_transient(HttpStatusCode statusCode, string expectedReason)
    {
        var fetcher = CreateFetcher((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(string.Empty) }));

        var result = await fetcher.FetchAsync(SourceUrl, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.Equal(expectedReason, result.FailureReason);
    }

    [Fact]
    public async Task FetchAsync_classifies_timeout_as_transient()
    {
        var fetcher = CreateFetcher((_, _) => throw new TaskCanceledException("simulated timeout"));

        var result = await fetcher.FetchAsync(SourceUrl, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.Equal("source_timeout", result.FailureReason);
    }

    [Fact]
    public async Task FetchAsync_classifies_connection_reset_as_transient()
    {
        var fetcher = CreateFetcher((_, _) => throw new HttpRequestException("simulated connection reset"));

        var result = await fetcher.FetchAsync(SourceUrl, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.Equal("source_unavailable", result.FailureReason);
    }

    [Fact]
    public async Task FetchAsync_classifies_empty_body_as_permanent()
    {
        var fetcher = CreateFetcher((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) }));

        var result = await fetcher.FetchAsync(SourceUrl, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal("source_empty", result.FailureReason);
    }

    private static WordPressWebhookPublisher CreatePublisher(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new WebhookOptions("http://stub.local/wp-json/ia-pipeline/v1/posts"),
            new WebhookSignatureService("classification-test-secret"));

    private static WebhookPublishRequest BuildPublishRequest() => new(
        Guid.NewGuid(),
        "https://example.com/article",
        "Title",
        "<p>Body</p>",
        "Excerpt",
        "gpt-4o-mini",
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task PublishAsync_classifies_webhook_401_as_permanent()
    {
        var publisher = CreatePublisher((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"invalid_signature"}"""),
            }));

        var result = await publisher.PublishAsync(BuildPublishRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal("webhook_unauthorized", result.FailureReason);
    }

    [Fact]
    public async Task PublishAsync_classifies_webhook_422_as_permanent()
    {
        var publisher = CreatePublisher((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)422)
            {
                Content = new StringContent("""{"error":"invalid_payload"}"""),
            }));

        var result = await publisher.PublishAsync(BuildPublishRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal("webhook_invalid_payload", result.FailureReason);
    }

    [Fact]
    public async Task PublishAsync_classifies_webhook_5xx_as_transient()
    {
        var publisher = CreatePublisher((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(string.Empty),
            }));

        var result = await publisher.PublishAsync(BuildPublishRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.Equal("webhook_http_503", result.FailureReason);
    }

    [Fact]
    public async Task PublishAsync_classifies_timeout_as_transient()
    {
        var publisher = CreatePublisher((_, _) => throw new TaskCanceledException("simulated timeout"));

        var result = await publisher.PublishAsync(BuildPublishRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.Equal("webhook_timeout", result.FailureReason);
    }

    [Fact]
    public async Task PublishAsync_classifies_connection_reset_as_transient()
    {
        var publisher = CreatePublisher((_, _) => throw new HttpRequestException("simulated connection reset"));

        var result = await publisher.PublishAsync(BuildPublishRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.Equal("webhook_unavailable", result.FailureReason);
    }
}
