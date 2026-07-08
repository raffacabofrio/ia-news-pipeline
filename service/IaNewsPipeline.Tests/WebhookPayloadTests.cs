using System.Net;
using System.Text.Json;
using IaNewsPipeline.Tests.TestSupport;
using IaNewsPipeline.Worker.Services;

namespace IaNewsPipeline.Tests;

/// <summary>
/// AC2: outbound webhook payload building is unit-tested here in isolation, asserting the exact JSON shape
/// from architecture Â§5.1 (job_id, source_url, title, content_html, excerpt, meta.model, meta.generated_at).
/// This exercises <see cref="WordPressWebhookPublisher"/> directly with a fake in-memory HTTP handler that
/// captures the outgoing request without any real network call -- no queue, no job store, no worker loop.
/// (S1.2's WorkerPipelineTests already proves the full receive-to-publish loop end to end; this file isolates
/// only the payload-building/signing seam, per the story's test boundary matrix.)
/// </summary>
public sealed class WebhookPayloadTests
{
    private static readonly Guid JobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset GeneratedAt = new(2026, 7, 7, 15, 30, 0, TimeSpan.Zero);

    private static WebhookPublishRequest BuildRequest() => new(
        JobId,
        "https://example.com/article",
        "Rewritten Title",
        "<p>Rewritten body</p>",
        "A short excerpt.",
        "gpt-4o-mini",
        GeneratedAt);

    [Fact]
    public async Task PublishAsync_sends_the_exact_contract_json_field_set()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"post_id":1,"post_url":"http://wp.local/?p=1","duplicate":false}"""),
            };
        });

        var publisher = new WordPressWebhookPublisher(
            new HttpClient(handler),
            new WebhookOptions("http://stub.local/wp-json/ia-pipeline/v1/posts"),
            new WebhookSignatureService("payload-test-secret"));

        var result = await publisher.PublishAsync(BuildRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedBody);

        using var json = JsonDocument.Parse(capturedBody!);
        var root = json.RootElement;

        Assert.Equal(JobId, root.GetProperty("job_id").GetGuid());
        Assert.Equal("https://example.com/article", root.GetProperty("source_url").GetString());
        Assert.Equal("Rewritten Title", root.GetProperty("title").GetString());
        Assert.Equal("<p>Rewritten body</p>", root.GetProperty("content_html").GetString());
        Assert.Equal("A short excerpt.", root.GetProperty("excerpt").GetString());
        Assert.Equal("gpt-4o-mini", root.GetProperty("meta").GetProperty("model").GetString());
        Assert.Equal("2026-07-07T15:30:00.0000000Z", root.GetProperty("meta").GetProperty("generated_at").GetString());

        // Exact field set: no extra, no missing top-level fields.
        var topLevelNames = root.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(
            new[] { "job_id", "source_url", "title", "content_html", "excerpt", "meta" },
            topLevelNames);
        var metaNames = root.GetProperty("meta").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "model", "generated_at" }, metaNames);
    }

    [Fact]
    public async Task PublishAsync_sends_content_type_json_and_hmac_headers_derived_from_the_exact_body()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"post_id":1,"post_url":"http://wp.local/?p=1","duplicate":false}"""),
            };
        });

        const string secret = "payload-test-secret";
        var publisher = new WordPressWebhookPublisher(
            new HttpClient(handler),
            new WebhookOptions("http://stub.local/wp-json/ia-pipeline/v1/posts"),
            new WebhookSignatureService(secret));

        await publisher.PublishAsync(BuildRequest(), CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("application/json; charset=utf-8", capturedRequest!.Content!.Headers.ContentType!.ToString());
        Assert.True(capturedRequest.Headers.Contains("X-Pipeline-Timestamp"));
        Assert.True(capturedRequest.Headers.Contains("X-Pipeline-Signature"));

        var timestamp = capturedRequest.Headers.GetValues("X-Pipeline-Timestamp").Single();
        var signature = capturedRequest.Headers.GetValues("X-Pipeline-Signature").Single();
        var expectedSignature = new WebhookSignatureService(secret).Compute(timestamp, capturedBody!);

        Assert.Equal($"sha256={expectedSignature}", signature);
    }
}
