namespace IaNewsPipeline.Worker.Services;

public sealed record RewriteResult(
    bool IsSuccess,
    bool IsTransient,
    string? FailureReason,
    RewrittenPost? Post)
{
    public static RewriteResult Success(RewrittenPost post) => new(true, false, null, post);
    public static RewriteResult PermanentFailure(string reason) => new(false, false, reason, null);
    public static RewriteResult TransientFailure(string reason) => new(false, true, reason, null);
}
