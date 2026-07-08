using IaNewsPipeline.Api.Jobs;
using IaNewsPipeline.Api.Queueing;
using IaNewsPipeline.Worker;
using IaNewsPipeline.Worker.Services;

namespace IaNewsPipeline.Worker;

public static class WorkerProgram
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // AC5 requires every operational log line to carry job_id. JobMessageProcessor attaches
        // job_id via logger.BeginScope, but the default console formatter drops scope data unless
        // IncludeScopes is explicitly enabled, so without this the job_id would never actually
        // reach the log output.
        builder.Logging.AddSimpleConsole(options => options.IncludeScopes = true);

        builder.Services.AddSingleton<IJobStore>(_ => new MySqlJobStore(GetRequiredSetting(builder.Configuration, "MYSQL_CONNECTION")));
        builder.Services.AddSingleton<IJobQueue>(_ => new SqsJobQueue(
            GetRequiredSetting(builder.Configuration, "SQS_ENDPOINT"),
            GetRequiredSetting(builder.Configuration, "QUEUE_NAME")));
        builder.Services.AddSingleton(new WebhookSignatureService(GetRequiredSetting(builder.Configuration, "PIPELINE_SHARED_SECRET")));
        builder.Services.AddSingleton(new OpenAiOptions(
            GetRequiredSetting(builder.Configuration, "OPENAI_API_KEY"),
            "gpt-4o-mini"));
        builder.Services.AddSingleton(new WebhookOptions(GetRequiredSetting(builder.Configuration, "WP_WEBHOOK_URL")));
        builder.Services.AddHttpClient<ISourceFetcher, HttpSourceFetcher>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            // Real publisher sites commonly reject requests with no User-Agent header (default
            // HttpClient sends none) as bot traffic -- e.g. Wikipedia returns 403. AC1 requires a
            // real, publicly reachable article URL to succeed end-to-end, so a descriptive UA is
            // required for the happy path to work against real-world sites, not just fixtures.
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; IaNewsPipelineBot/1.0; +https://github.com/raffacabofrio/ia-news-pipeline)");
        });
        builder.Services.AddHttpClient<IOpenAiRewriteClient, OpenAiRewriteClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
            // AC1 (S4.3 live verification): observed real gpt-4o-mini completions taking 40-70s+ even
            // for short articles. A 60s client timeout was aborting genuinely-in-flight, successful
            // requests as "openai_timeout" -- and because a transient failure here only recovers via
            // the 120s SQS visibility timeout (SqsJobQueue.VisibilityTimeoutSeconds, frozen by
            // architecture D2), a single premature abort reliably blew AC1's 2-minute publish SLA.
            // 100s gives real responses room to land on the first attempt while keeping worst-case
            // total time (timeout + fetch/extract/publish overhead) comfortably under 120s.
            client.Timeout = TimeSpan.FromSeconds(100);
        });
        builder.Services.AddHttpClient<IWebhookPublisher, WordPressWebhookPublisher>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<IArticleExtractor, SmartReaderArticleExtractor>();
        builder.Services.AddScoped<JobMessageProcessor>();
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }

    private static string GetRequiredSetting(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Missing required configuration value '{key}'.")
            : value;
    }
}
