using SmartReader;

namespace IaNewsPipeline.Worker.Services;

public sealed class SmartReaderArticleExtractor : IArticleExtractor
{
    public Task<ExtractionResult> ExtractAsync(Uri sourceUrl, string html, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Use the instance-based Reader API rather than the static Reader.ParseArticle helper: the static
        // helper throws an internal FormatException for every input in SmartReader 0.11.0 (swallowed into
        // Article.Errors, silently forcing IsReadable=false regardless of content), which made extraction
        // permanently non-functional. The instance API produces the correct, documented result. [S1.3]
        var article = new Reader(sourceUrl.ToString(), html).GetArticle();

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
