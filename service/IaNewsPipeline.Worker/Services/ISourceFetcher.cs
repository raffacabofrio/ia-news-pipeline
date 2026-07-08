namespace IaNewsPipeline.Worker.Services;

public interface ISourceFetcher
{
    Task<FetchResult> FetchAsync(Uri sourceUrl, CancellationToken cancellationToken);
}
