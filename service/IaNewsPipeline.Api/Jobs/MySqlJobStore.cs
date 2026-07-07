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
            SELECT job_id, state, source_url, published_post_url, failure_reason
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

        return new JobRecord(
            reader.GetGuid(jobIdOrdinal),
            reader.GetString(stateOrdinal),
            reader.GetString(sourceUrlOrdinal),
            reader.IsDBNull(publishedPostUrlOrdinal) ? null : reader.GetString(publishedPostUrlOrdinal),
            reader.IsDBNull(failureReasonOrdinal) ? null : reader.GetString(failureReasonOrdinal));
    }

    public async Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE pipeline.jobs
            SET state = @state,
                failure_reason = @failure_reason,
                updated_at = UTC_TIMESTAMP()
            WHERE job_id = @job_id;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@job_id", jobId.ToString());
        command.Parameters.AddWithValue("@state", JobStates.Failed);
        command.Parameters.AddWithValue("@failure_reason", error);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
