using System.Text.Json.Serialization;

namespace IaNewsPipeline.Api.Contracts;

public sealed record GeneratePostRequest(
    [property: JsonPropertyName("url")] string? Url);

public sealed record GeneratePostAcceptedResponse(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("status_url")] string StatusUrl);

public sealed record JobStatusResponse(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("post_url")] string? PostUrl,
    [property: JsonPropertyName("error")] string? Error);
