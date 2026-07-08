namespace IaNewsPipeline.Worker.Services;

public sealed record ExtractedArticle(string Title, string ContentHtml, string Excerpt);
