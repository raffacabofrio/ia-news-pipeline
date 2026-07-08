using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IaNewsPipeline.Worker.Services;

public sealed class WordPressWebhookPublisher(
    HttpClient httpClient,
    WebhookOptions options,
    WebhookSignatureService signatureService) : IWebhookPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<PublishResult> PublishAsync(WebhookPublishRequest request, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            job_id = request.JobId,
            source_url = request.SourceUrl,
            title = request.Title,
            content_html = request.ContentHtml,
            excerpt = request.Excerpt,
            meta = new
            {
                model = request.Model,
                generated_at = request.GeneratedAt.UtcDateTime.ToString("O")
            }
        });

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = signatureService.Compute(timestamp, body);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        httpRequest.Headers.Add("X-Pipeline-Timestamp", timestamp);
        httpRequest.Headers.Add("X-Pipeline-Signature", $"sha256={signature}");

        try
        {
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if ((int)response.StatusCode >= 500)
            {
                return PublishResult.TransientFailure($"webhook_http_{(int)response.StatusCode}");
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return PublishResult.PermanentFailure("webhook_unauthorized");
            }

            if ((int)response.StatusCode == 422)
            {
                return PublishResult.PermanentFailure("webhook_invalid_payload");
            }

            if (!response.IsSuccessStatusCode)
            {
                return PublishResult.PermanentFailure($"webhook_http_{(int)response.StatusCode}");
            }

            var payload = await response.Content.ReadFromJsonAsync<WebhookResponse>(SerializerOptions, cancellationToken);

            return string.IsNullOrWhiteSpace(payload?.PostUrl)
                ? PublishResult.PermanentFailure("webhook_missing_post_url")
                : PublishResult.Success(payload.PostUrl);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PublishResult.TransientFailure("webhook_timeout");
        }
        catch (HttpRequestException)
        {
            return PublishResult.TransientFailure("webhook_unavailable");
        }
    }

    private sealed class WebhookResponse
    {
        [JsonPropertyName("post_url")]
        public string? PostUrl { get; init; }
    }
}
