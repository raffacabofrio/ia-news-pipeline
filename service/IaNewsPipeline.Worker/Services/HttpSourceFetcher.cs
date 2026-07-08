using System.Net;

namespace IaNewsPipeline.Worker.Services;

public sealed class HttpSourceFetcher(HttpClient httpClient) : ISourceFetcher
{
    public async Task<FetchResult> FetchAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(sourceUrl, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return FetchResult.PermanentFailure("source_not_found");
            }

            if ((int)response.StatusCode >= 500)
            {
                return FetchResult.TransientFailure($"source_http_{(int)response.StatusCode}");
            }

            if ((int)response.StatusCode >= 400)
            {
                return FetchResult.PermanentFailure($"source_http_{(int)response.StatusCode}");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(html)
                ? FetchResult.PermanentFailure("source_empty")
                : FetchResult.Success(html);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FetchResult.TransientFailure("source_timeout");
        }
        catch (HttpRequestException)
        {
            return FetchResult.TransientFailure("source_unavailable");
        }
    }
}
