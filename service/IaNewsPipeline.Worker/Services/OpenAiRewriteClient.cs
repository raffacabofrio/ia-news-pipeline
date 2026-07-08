using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IaNewsPipeline.Worker.Services;

public sealed class OpenAiRewriteClient(
    HttpClient httpClient,
    OpenAiOptions options) : IOpenAiRewriteClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<RewriteResult> RewriteAsync(
        Uri sourceUrl,
        ExtractedArticle article,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = options.Model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "Rewrite the article into JSON with exactly title, content_html, and excerpt."
                },
                new
                {
                    role = "user",
                    content = $"""
                        Source URL: {sourceUrl}
                        Original title: {article.Title}
                        Original excerpt: {article.Excerpt}
                        Original content:
                        {article.ContentHtml}
                        """
                }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "rewritten_post",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            title = new { type = "string" },
                            content_html = new { type = "string" },
                            excerpt = new { type = "string" }
                        },
                        required = new[] { "title", "content_html", "excerpt" }
                    }
                }
            }
        });

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return RewriteResult.TransientFailure($"openai_http_{(int)response.StatusCode}");
            }

            if (!response.IsSuccessStatusCode)
            {
                return RewriteResult.PermanentFailure($"openai_http_{(int)response.StatusCode}");
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenAiResponse>(SerializerOptions, cancellationToken);
            var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return RewriteResult.PermanentFailure("openai_invalid_output");
            }

            RewritePayload? parsed;

            try
            {
                parsed = JsonSerializer.Deserialize<RewritePayload>(content, SerializerOptions);
            }
            catch (JsonException)
            {
                return RewriteResult.PermanentFailure("openai_invalid_output");
            }

            if (parsed is null ||
                string.IsNullOrWhiteSpace(parsed.Title) ||
                string.IsNullOrWhiteSpace(parsed.ContentHtml) ||
                string.IsNullOrWhiteSpace(parsed.Excerpt))
            {
                return RewriteResult.PermanentFailure("openai_invalid_output");
            }

            return RewriteResult.Success(new RewrittenPost(
                parsed.Title.Trim(),
                parsed.ContentHtml.Trim(),
                parsed.Excerpt.Trim(),
                options.Model,
                DateTimeOffset.UtcNow));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RewriteResult.TransientFailure("openai_timeout");
        }
        catch (HttpRequestException)
        {
            return RewriteResult.TransientFailure("openai_unavailable");
        }
    }

    private sealed class OpenAiResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; init; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; init; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed class RewritePayload
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("content_html")]
        public string? ContentHtml { get; init; }

        [JsonPropertyName("excerpt")]
        public string? Excerpt { get; init; }
    }
}
