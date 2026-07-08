namespace IaNewsPipeline.Worker.Services;

public sealed record PublishResult(
    bool IsSuccess,
    bool IsTransient,
    string? FailureReason,
    string? PostUrl)
{
    public static PublishResult Success(string postUrl) => new(true, false, null, postUrl);
    public static PublishResult PermanentFailure(string reason) => new(false, false, reason, null);
    public static PublishResult TransientFailure(string reason) => new(false, true, reason, null);
}
