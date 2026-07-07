namespace IaNewsPipeline.Api.Jobs;

public static class JobStates
{
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Publishing = "publishing";
    public const string Published = "published";
    public const string Failed = "failed";
}
