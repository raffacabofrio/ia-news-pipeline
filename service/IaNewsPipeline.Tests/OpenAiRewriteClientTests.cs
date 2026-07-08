using System.Text.Json;
using IaNewsPipeline.Tests.TestSupport;
using IaNewsPipeline.Worker.Services;

namespace IaNewsPipeline.Tests;

public sealed class OpenAiRewriteClientTests
{
    [Fact]
    public async Task RewriteAsync_strips_html_and_truncates_large_source_material_before_calling_openai()
    {
        var oversizedHtml = "<p>" + new string('A', 14_000) + "</p><p>TAILMARKER</p>";
        var article = new ExtractedArticle("Original title", oversizedHtml, "Original excerpt");

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(requestBody);
            var userPrompt = json.RootElement
                .GetProperty("messages")[1]
                .GetProperty("content")
                .GetString();

            Assert.NotNull(userPrompt);
            Assert.DoesNotContain("<p>", userPrompt, StringComparison.Ordinal);
            Assert.DoesNotContain("TAILMARKER", userPrompt, StringComparison.Ordinal);
            Assert.Contains("[truncated]", userPrompt, StringComparison.Ordinal);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"title\":\"Rewritten\",\"content_html\":\"<p>Body</p>\",\"excerpt\":\"Summary\"}"}}]}"""),
            };
        });

        var client = new OpenAiRewriteClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/"),
            },
            new OpenAiOptions("test-api-key", "gpt-4o-mini"));

        var result = await client.RewriteAsync(
            new Uri("https://example.com/article"),
            article,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rewritten", result.Post!.Title);
        Assert.Equal("<p>Body</p>", result.Post.ContentHtml);
        Assert.Equal("Summary", result.Post.Excerpt);
    }
}
