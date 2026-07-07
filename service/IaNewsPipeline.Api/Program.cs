using System.Text.Json;
using IaNewsPipeline.Api.Contracts;
using IaNewsPipeline.Api.Jobs;
using IaNewsPipeline.Api.Queueing;
using IaNewsPipeline.Api.Security;
using IaNewsPipeline.Api.Services;
using IaNewsPipeline.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(new HmacRequestValidator(GetRequiredSetting(builder.Configuration, "PIPELINE_SHARED_SECRET")));
builder.Services.AddSingleton<IJobStore>(_ => new MySqlJobStore(GetRequiredSetting(builder.Configuration, "MYSQL_CONNECTION")));
builder.Services.AddSingleton<IJobQueue>(_ => new SqsJobQueue(
    GetRequiredSetting(builder.Configuration, "SQS_ENDPOINT"),
    GetRequiredSetting(builder.Configuration, "QUEUE_NAME")));
builder.Services.AddSingleton<JobIntakeService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    component = "api",
}));

app.MapPost("/api/generate-post", async (
    HttpRequest request,
    HmacRequestValidator hmacValidator,
    JobIntakeService intakeService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("GeneratePost");
    var body = await ReadRawBodyAsync(request, cancellationToken);

    if (!hmacValidator.IsValid(request.Headers, body))
    {
        return Results.Unauthorized();
    }

    GeneratePostRequest? payload;

    try
    {
        payload = JsonSerializer.Deserialize<GeneratePostRequest>(body);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "invalid_request" });
    }

    if (payload?.Url is null || !PublicUrlValidator.TryParse(payload.Url, out var sourceUrl))
    {
        return Results.BadRequest(new { error = "invalid_url" });
    }

    var result = await intakeService.CreateJobAsync(sourceUrl!, cancellationToken);

    if (!result.Succeeded)
    {
        logger.LogError(
            "Failed to enqueue job after persistence. job_id={JobId} reason={Reason}",
            result.Job?.JobId,
            result.FailureReason);

        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Failed to enqueue job");
    }

    using var scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["job_id"] = result.Job!.JobId,
    });

    logger.LogInformation("Accepted generate-post request");

    return Results.Accepted(
        $"/api/jobs/{result.Job.JobId}",
        new GeneratePostAcceptedResponse(
            result.Job.JobId.ToString(),
            $"/api/jobs/{result.Job.JobId}"));
});

app.MapGet("/api/jobs/{id:guid}", async (Guid id, IJobStore jobStore, CancellationToken cancellationToken) =>
{
    var job = await jobStore.GetJobAsync(id, cancellationToken);

    return job is null
        ? Results.NotFound()
        : Results.Ok(new JobStatusResponse(
            job.JobId.ToString(),
            job.State,
            job.PublishedPostUrl,
            job.Error));
});

app.Run();

static string GetRequiredSetting(IConfiguration configuration, string key)
{
    var value = configuration[key];
    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException($"Missing required configuration value '{key}'.")
        : value;
}

static async Task<string> ReadRawBodyAsync(HttpRequest request, CancellationToken cancellationToken)
{
    using var reader = new StreamReader(request.Body);
    return await reader.ReadToEndAsync(cancellationToken);
}

public partial class Program;
