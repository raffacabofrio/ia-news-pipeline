using IaNewsPipeline.Worker.Services;

namespace IaNewsPipeline.Tests;

/// <summary>
/// AC1: extraction normalization logic (SmartReader output to normalized article fields, empty/non-article
/// detection) is unit-tested here in isolation from any HTTP fetch, queue, or worker loop. These tests call
/// <see cref="SmartReaderArticleExtractor"/> directly against literal HTML strings.
/// </summary>
public sealed class ArticleExtractionTests
{
    private static readonly Uri SourceUrl = new("https://example.com/bakeries/sourdough-revival");

    private const string ArticleLikeHtml = """
        <html>
        <head><title>How Local Bakeries Are Reinventing Sourdough</title></head>
        <body>
        <div id="nav"><a href="/">Home</a><a href="/about">About</a></div>
        <div id="content">
        <article>
        <h1>How Local Bakeries Are Reinventing Sourdough</h1>
        <p>Across the city, small neighborhood bakeries are rediscovering the slow art of sourdough
        fermentation, trading commercial yeast for wild cultures that take days instead of hours to rise.
        Bakers say the shift began during a wave of renewed interest in traditional baking methods, driven
        partly by home bakers who took up the hobby and later turned it into a full-time trade.</p>
        <p>The process starts with a starter culture that is fed daily with flour and water, encouraging
        naturally occurring yeast and bacteria to flourish. Over several days, the starter becomes active
        enough to leaven a full loaf, giving the bread its distinctive tang and chewy crumb. Maintaining a
        healthy starter requires daily attention, consistent temperatures, and a willingness to discard and
        refresh a portion of it every single day without exception.</p>
        <p>Owners of these bakeries report that customers are willing to pay a premium for loaves made this
        way, citing better digestibility and a deeper flavor than mass-produced bread. Several shops have
        started offering baking classes to meet demand for the technique, often booking out weeks in advance
        as home bakers try to replicate the same tangy, open-crumbed loaves they buy from these shops.</p>
        <p>Industry analysts note that the trend mirrors a broader consumer shift toward artisanal, small-batch
        food production across many categories, not just bread. Bakeries that adapted early say the investment
        in time has paid off in loyal repeat customers who now visit multiple times a week, sometimes tracking
        the bakery's fermentation schedule the way other shoppers track a favorite restaurant's daily specials.</p>
        <p>Some bakers caution that the transition is not simple. Wild-yeast fermentation is far less predictable
        than commercial yeast, and a single batch can fail because of humidity, flour quality, or a starter that
        was fed too early or too late. Bakeries that stuck with the method through early failures say the
        consistency eventually came, but only after months of careful record keeping and repeated experimentation
        with hydration ratios, proofing times, and oven temperatures.</p>
        </article>
        </div>
        </body>
        </html>
        """;

    private const string NonArticleHtml = """
        <html>
        <head><title>Empty</title></head>
        <body>
        <div id="nav"><a href="/">Home</a><a href="/login">Login</a></div>
        <div id="footer">Copyright 2026</div>
        </body>
        </html>
        """;

    [Fact]
    public async Task ExtractAsync_normalizes_article_like_html_into_title_content_and_excerpt()
    {
        var extractor = new SmartReaderArticleExtractor();

        var result = await extractor.ExtractAsync(SourceUrl, ArticleLikeHtml, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.FailureReason);
        Assert.NotNull(result.Article);
        Assert.Equal("How Local Bakeries Are Reinventing Sourdough", result.Article!.Title);
        Assert.Contains("sourdough", result.Article.ContentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fermentation", result.Article.ContentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p", result.Article.ContentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.Article.Excerpt));
        Assert.Contains("bakeries", result.Article.Excerpt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_classifies_empty_non_article_html_as_permanent_failure()
    {
        var extractor = new SmartReaderArticleExtractor();

        var result = await extractor.ExtractAsync(SourceUrl, NonArticleHtml, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Article);
        Assert.Equal("source_not_article", result.FailureReason);
    }
}
