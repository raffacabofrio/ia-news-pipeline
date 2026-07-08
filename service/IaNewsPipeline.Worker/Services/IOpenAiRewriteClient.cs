namespace IaNewsPipeline.Worker.Services;

public interface IOpenAiRewriteClient
{
    Task<RewriteResult> RewriteAsync(
        Uri sourceUrl,
        ExtractedArticle article,
        CancellationToken cancellationToken);
}
