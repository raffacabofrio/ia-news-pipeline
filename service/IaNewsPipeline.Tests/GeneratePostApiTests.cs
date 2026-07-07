using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using IaNewsPipeline.Api.Contracts;
using IaNewsPipeline.Api.Jobs;
using IaNewsPipeline.Api.Queueing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IaNewsPipeline.Tests;

public sealed class GeneratePostApiTests
{
    [Fact]
    public async Task Post_generate_post_returns_202_with_job_contract_when_request_is_valid()
    {
        var store = new FakeJobStore();
        var queue = new FakeJobQueue();

        await using var factory = new TestApiFactory(store, queue);
        using var client = factory.CreateClient();

        const string payload = """{"url":"https://example.com/article"}""";
        using var request = SignedRequest(payload);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GeneratePostAcceptedResponse>();

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.JobId));
        Assert.Equal($"/api/jobs/{body.JobId}", body.StatusUrl);
        Assert.Single(store.CreatedJobs);
        Assert.Equal(body.JobId, store.CreatedJobs.Single().JobId.ToString());
        Assert.Single(queue.EnqueuedJobIds);
        Assert.Equal(body.JobId, queue.EnqueuedJobIds.Single().ToString());
    }

    [Fact]
    public async Task Post_generate_post_returns_401_when_signature_is_missing()
    {
        await using var factory = new TestApiFactory(new FakeJobStore(), new FakeJobQueue());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/generate-post",
            new { url = "https://example.com/article" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_generate_post_returns_401_when_signature_is_invalid()
    {
        await using var factory = new TestApiFactory(new FakeJobStore(), new FakeJobQueue());
        using var client = factory.CreateClient();

        using var request = SignedRequest("""{"url":"https://example.com/article"}""");
        request.Headers.Remove("X-Pipeline-Signature");
        request.Headers.Add("X-Pipeline-Signature", "sha256=deadbeef");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"url":"notaurl"}""")]
    [InlineData("""{"url":"http://localhost/article"}""")]
    [InlineData("""{}""")]
    public async Task Post_generate_post_returns_400_for_invalid_payload(string payload)
    {
        var store = new FakeJobStore();
        var queue = new FakeJobQueue();

        await using var factory = new TestApiFactory(store, queue);
        using var client = factory.CreateClient();
        using var request = SignedRequest(payload);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(store.CreatedJobs);
        Assert.Empty(queue.EnqueuedJobIds);
    }

    [Fact]
    public async Task Get_job_returns_404_for_unknown_job()
    {
        await using var factory = new TestApiFactory(new FakeJobStore(), new FakeJobQueue());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/jobs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_job_returns_200_with_job_contract_for_known_job()
    {
        var jobId = Guid.NewGuid();
        var store = new FakeJobStore();
        store.Seed(new JobRecord(
            jobId,
            JobStates.Published,
            "https://example.com/article",
            "https://wordpress.local/post/1",
            null));

        await using var factory = new TestApiFactory(store, new FakeJobQueue());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/jobs/{jobId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JobStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal(jobId.ToString(), body.JobId);
        Assert.Equal(JobStates.Published, body.State);
        Assert.Equal("https://wordpress.local/post/1", body.PostUrl);
        Assert.Null(body.Error);
    }

    [Fact]
    public async Task Post_generate_post_returns_500_and_marks_job_failed_when_enqueue_fails()
    {
        var store = new FakeJobStore();
        var queue = new FakeJobQueue { ThrowOnEnqueue = true };

        await using var factory = new TestApiFactory(store, queue);
        using var client = factory.CreateClient();
        using var request = SignedRequest("""{"url":"https://example.com/article"}""");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Single(store.CreatedJobs);

        var job = store.CreatedJobs.Single();
        Assert.Equal(JobStates.Failed, job.State);
        Assert.Equal("enqueue_failed", job.Error);
    }

    private static HttpRequestMessage SignedRequest(string payload)
    {
        const string timestamp = "1720310400";
        const string secret = "test-shared-secret";

        var signature = ComputeSignature(secret, timestamp, payload);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate-post")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        request.Headers.Add("X-Pipeline-Timestamp", timestamp);
        request.Headers.Add("X-Pipeline-Signature", $"sha256={signature}");

        return request;
    }

    private static string ComputeSignature(string secret, string timestamp, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return Convert.ToHexStringLower(hash);
    }

    private sealed class TestApiFactory(FakeJobStore store, FakeJobQueue queue)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Environment.SetEnvironmentVariable("PIPELINE_SHARED_SECRET", "test-shared-secret");
            Environment.SetEnvironmentVariable("MYSQL_CONNECTION", "Server=fake;");
            Environment.SetEnvironmentVariable("SQS_ENDPOINT", "http://elasticmq:9324");
            Environment.SetEnvironmentVariable("QUEUE_NAME", "pipeline-jobs");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IJobStore>();
                services.RemoveAll<IJobQueue>();
                services.AddSingleton<IJobStore>(store);
                services.AddSingleton<IJobQueue>(queue);
            });
        }
    }

    private sealed class FakeJobQueue : IJobQueue
    {
        public List<Guid> EnqueuedJobIds { get; } = [];
        public bool ThrowOnEnqueue { get; init; }

        public Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken)
        {
            if (ThrowOnEnqueue)
            {
                throw new InvalidOperationException("Queue unavailable");
            }

            EnqueuedJobIds.Add(jobId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJobStore : IJobStore
    {
        public List<JobRecord> CreatedJobs { get; } = [];

        public void Seed(JobRecord job) => CreatedJobs.Add(job);

        public Task<JobRecord> CreateQueuedJobAsync(Uri sourceUrl, CancellationToken cancellationToken)
        {
            var job = new JobRecord(
                Guid.NewGuid(),
                JobStates.Queued,
                sourceUrl.ToString(),
                null,
                null);

            CreatedJobs.Add(job);
            return Task.FromResult(job);
        }

        public Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreatedJobs.SingleOrDefault(job => job.JobId == jobId));
        }

        public Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken)
        {
            var index = CreatedJobs.FindIndex(job => job.JobId == jobId);
            var current = CreatedJobs[index];
            CreatedJobs[index] = current with { State = JobStates.Failed, Error = error };
            return Task.CompletedTask;
        }
    }
}
