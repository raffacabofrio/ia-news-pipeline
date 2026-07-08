using SmartReader;

namespace IaNewsPipeline.Worker.Services;

public sealed class SmartReaderArticleExtractor : IArticleExtractor
{
    public Task<ExtractionResult> ExtractAsync(Uri sourceUrl, string html, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var article = Reader.ParseArticle(sourceUrl.ToString(), html);

        if (!article.IsReadable || string.IsNullOrWhiteSpace(article.Content))
        {
            return Task.FromResult(ExtractionResult.PermanentFailure("source_not_article"));
        }

        var excerpt = string.IsNullOrWhiteSpace(article.Excerpt)
            ? BuildExcerpt(article.TextContent)
            : article.Excerpt.Trim();

        return Task.FromResult(ExtractionResult.Success(new ExtractedArticle(
            string.IsNullOrWhiteSpace(article.Title) ? "Untitled article" : article.Title.Trim(),
            article.Content,
            excerpt)));
    }

    private static string BuildExcerpt(string? textContent)
    {
        if (string.IsNullOrWhiteSpace(textContent))
        {
            return string.Empty;
        }

        var normalized = string.Join(" ", textContent
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return normalized.Length <= 280
            ? normalized
            : normalized[..280].TrimEnd() + "...";
    }
}
