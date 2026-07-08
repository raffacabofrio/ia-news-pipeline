namespace IaNewsPipeline.Api.Jobs;

public sealed record JobRecord(
    Guid JobId,
    string State,
    string SourceUrl,
    string? PublishedPostUrl,
    string? Error,
    string? RewriteModel = null,
    DateTimeOffset? GeneratedAt = null);
