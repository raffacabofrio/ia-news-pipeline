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
        });
        builder.Services.AddHttpClient<IOpenAiRewriteClient, OpenAiRewriteClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
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
