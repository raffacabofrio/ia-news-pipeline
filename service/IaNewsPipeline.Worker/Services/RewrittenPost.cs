namespace IaNewsPipeline.Worker.Services;

public sealed record RewrittenPost(
    string Title,
    string ContentHtml,
    string Excerpt,
    string Model,
    DateTimeOffset GeneratedAt);
