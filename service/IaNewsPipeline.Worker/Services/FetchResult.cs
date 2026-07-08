namespace IaNewsPipeline.Worker.Services;

public sealed record FetchResult(
    bool IsSuccess,
    bool IsTransient,
    string? Html,
    string? FailureReason)
{
    public static FetchResult Success(string html) => new(true, false, html, null);
    public static FetchResult PermanentFailure(string reason) => new(false, false, null, reason);
    public static FetchResult TransientFailure(string reason) => new(false, true, null, reason);
}
