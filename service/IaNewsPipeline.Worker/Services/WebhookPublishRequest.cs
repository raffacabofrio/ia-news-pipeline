namespace IaNewsPipeline.Worker.Services;

public sealed record WebhookPublishRequest(
    Guid JobId,
    string SourceUrl,
    string Title,
    string ContentHtml,
    string Excerpt,
    string Model,
    DateTimeOffset GeneratedAt);
