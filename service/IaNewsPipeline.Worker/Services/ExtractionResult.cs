namespace IaNewsPipeline.Worker.Services;

public sealed record ExtractionResult(
    bool IsSuccess,
    ExtractedArticle? Article,
    string? FailureReason)
{
    public static ExtractionResult Success(ExtractedArticle article) => new(true, article, null);
    public static ExtractionResult PermanentFailure(string reason) => new(false, null, reason);
}
