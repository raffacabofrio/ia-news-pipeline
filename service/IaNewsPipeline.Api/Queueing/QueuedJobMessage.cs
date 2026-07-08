namespace IaNewsPipeline.Api.Queueing;

public sealed record QueuedJobMessage(string ReceiptHandle, string Body);
