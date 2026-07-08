namespace IaNewsPipeline.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="HttpMessageHandler"/> for isolating HTTP-client-based services (S1.3) from any
/// real network call. No sockets are opened; the supplied delegate intercepts <c>SendAsync</c> directly,
/// so it can also be used to simulate transport failures (e.g. throw <see cref="TaskCanceledException"/>
/// or <see cref="HttpRequestException"/>) for transient-vs-permanent classification tests.
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : this((request, _) => Task.FromResult(handler(request)))
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => handler(request, cancellationToken);
}
