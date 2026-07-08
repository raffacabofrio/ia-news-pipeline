using MySqlConnector;

namespace IaNewsPipeline.Api.Jobs;

public sealed class MySqlJobStore(string connectionString) : IJobStore
{
    public async Task<JobRecord> CreateQueuedJobAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        var job = new JobRecord(Guid.NewGuid(), JobStates.Queued, sourceUrl.ToString(), null, null);

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO pipeline.jobs (job_id, state, source_url)
            VALUES (@job_id, @state, @source_url);
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@job_id", job.JobId.ToString());
        command.Parameters.AddWithValue("@state", job.State);
        command.Parameters.AddWithValue("@source_url", job.SourceUrl);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return job;
    }

    public async Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT job_id, state, source_url, published_post_url, failure_reason, rewrite_model, generated_at_utc
            FROM pipeline.jobs
            WHERE job_id = @job_id
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@job_id", jobId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var jobIdOrdinal = reader.GetOrdinal("job_id");
        var stateOrdinal = reader.GetOrdinal("state");
        var sourceUrlOrdinal = reader.GetOrdinal("source_url");
        var publishedPostUrlOrdinal = reader.GetOrdinal("published_post_url");
        var failureReasonOrdinal = reader.GetOrdinal("failure_reason");
        var rewriteModelOrdinal = reader.GetOrdinal("rewrite_model");
        var generatedAtOrdinal = reader.GetOrdinal("generated_at_utc");

        return new JobRecord(
            reader.GetGuid(jobIdOrdinal),
            reader.GetString(stateOrdinal),
            reader.GetString(sourceUrlOrdinal),
            reader.IsDBNull(publishedPostUrlOrdinal) ? null : reader.GetString(publishedPostUrlOrdinal),
            reader.IsDBNull(failureReasonOrdinal) ? null : reader.GetString(failureReasonOrdinal),
            reader.IsDBNull(rewriteModelOrdinal) ? null : reader.GetString(rewriteModelOrdinal),
            reader.IsDBNull(generatedAtOrdinal)
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(generatedAtOrdinal), DateTimeKind.Utc)));
    }

    public Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken)
    {
        return UpdateStateAsync(
            jobId,
            JobStates.Processing,
            publishedPostUrl: null,
            failureReason: null,
            rewriteModel: null,
            generatedAt: null,
            cancellationToken);
    }

    public Task MarkPublishingAsync(
        Guid jobId,
        string rewriteModel,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        return UpdateStateAsync(
            jobId,
            JobStates.Publishing,
            publishedPostUrl: null,
            failureReason: null,
            rewriteModel,
            generatedAt,
            cancellationToken);
    }

    public Task MarkPublishedAsync(
        Guid jobId,
        string postUrl,
        string rewriteModel,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        return UpdateStateAsync(
            jobId,
            JobStates.Published,
            publishedPostUrl: postUrl,
            failureReason: null,
            rewriteModel,
            generatedAt,
            cancellationToken);
    }

    public async Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken)
    {
        await UpdateStateAsync(
            jobId,
            JobStates.Failed,
            publishedPostUrl: null,
            failureReason: error,
            rewriteModel: null,
            generatedAt: null,
            cancellationToken);
    }

    private async Task UpdateStateAsync(
        Guid jobId,
        string state,
        string? publishedPostUrl,
        string? failureReason,
        string? rewriteModel,
        DateTimeOffset? generatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE pipeline.jobs
            SET state = @state,
                published_post_url = CASE
                    WHEN @published_post_url_is_null = 1 THEN published_post_url
                    ELSE @published_post_url
                END,
                failure_reason = CASE
                    WHEN @failure_reason_is_null = 1 THEN NULL
                    ELSE @failure_reason
                END,
                rewrite_model = CASE
                    WHEN @rewrite_model_is_null = 1 THEN rewrite_model
                    ELSE @rewrite_model
                END,
                generated_at_utc = CASE
                    WHEN @generated_at_is_null = 1 THEN generated_at_utc
                    ELSE @generated_at_utc
                END,
                updated_at = UTC_TIMESTAMP()
            WHERE job_id = @job_id;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@job_id", jobId.ToString());
        command.Parameters.AddWithValue("@state", state);
        command.Parameters.AddWithValue("@published_post_url", publishedPostUrl);
        command.Parameters.AddWithValue("@published_post_url_is_null", publishedPostUrl is null ? 1 : 0);
        command.Parameters.AddWithValue("@failure_reason", failureReason);
        command.Parameters.AddWithValue("@failure_reason_is_null", failureReason is null ? 1 : 0);
        command.Parameters.AddWithValue("@rewrite_model", rewriteModel);
        command.Parameters.AddWithValue("@rewrite_model_is_null", rewriteModel is null ? 1 : 0);
        command.Parameters.AddWithValue("@generated_at_utc", generatedAt?.UtcDateTime);
        command.Parameters.AddWithValue("@generated_at_is_null", generatedAt is null ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
