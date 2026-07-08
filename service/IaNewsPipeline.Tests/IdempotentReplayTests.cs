using System.Net;
using IaNewsPipeline.Tests.TestSupport;
using IaNewsPipeline.Worker.Services;

namespace IaNewsPipeline.Tests;

/// <summary>
/// AC5: idempotent replay handling. Proves that a webhook response of 200 with duplicate:true is classified
/// as terminal success (not retried, not failed) by the response-interpretation logic inside
/// <see cref="WordPressWebhookPublisher.PublishAsync"/>, isolated from the stub HTTP transport, the queue,
/// and the job store. (S1.2's WorkerPipelineTests separately proves the full worker loop ends in the
/// "published" state and deletes the queue message for this same scenario -- this file isolates only the
/// publisher's response-interpretation decision, per the story's test boundary matrix.)
/// </summary>
public sealed class IdempotentReplayTests
{
    private static WordPressWebhookPublisher CreatePublisher(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new WebhookOptions("http://stub.local/wp-json/ia-pipeline/v1/posts"),
            new WebhookSignatureService("replay-test-secret"));

    private static WebhookPublishRequest BuildPublishRequest() => new(
        Guid.NewGuid(),
        "https://example.com/article",
        "Title",
        "<p>Body</p>",
        "Excerpt",
        "gpt-4o-mini",
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task PublishAsync_treats_200_duplicate_true_as_terminal_success_not_a_failure()
    {
        var publisher = CreatePublisher((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"post_id":42,"post_url":"http://wp.local/?p=42","duplicate":true}"""),
            }));

        var result = await publisher.PublishAsync(BuildPublishRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Null(result.FailureReason);
        Assert.Equal("http://wp.local/?p=42", result.PostUrl);
    }

    [Fact]
    public async Task PublishAsync_treats_201_duplicate_false_as_terminal_success_for_comparison()
    {
        // Companion case: the first-time publish (duplicate:false, 201) must land on the exact same
        // success branch as the duplicate-replay case above -- proving "duplicate" is not a special code
        // path that could regress independently of ordinary success handling.
        var publisher = CreatePublisher((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"post_id":43,"post_url":"http://wp.local/?p=43","duplicate":false}"""),
            }));

        var result = await publisher.PublishAsync(BuildPublishRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal("http://wp.local/?p=43", result.PostUrl);
    }

    [Fact]
    public async Task PublishAsync_treats_200_success_without_post_url_as_permanent_failure()
    {
        // Boundary case for the same response-interpretation function: a 200 that is missing the post_url
        // the contract requires must not be silently treated as success just because the status was 2xx.
        var publisher = CreatePublisher((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"duplicate":true}"""),
            }));

        var result = await publisher.PublishAsync(BuildPublishRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal("webhook_missing_post_url", result.FailureReason);
    }
}
