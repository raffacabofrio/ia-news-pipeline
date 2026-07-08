namespace IaNewsPipeline.Worker.Services;

public interface IArticleExtractor
{
    Task<ExtractionResult> ExtractAsync(Uri sourceUrl, string html, CancellationToken cancellationToken);
}
