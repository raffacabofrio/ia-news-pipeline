using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace IaNewsPipeline.Api.Queueing;

public sealed class SqsJobQueue : IJobQueue
{
    private readonly AmazonSQSClient _client;
    private readonly string _queueName;
    private string? _queueUrl;

    public SqsJobQueue(string endpoint, string queueName)
    {
        _queueName = queueName;
        _client = CreateClient(endpoint);
    }

    public async Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _queueUrl ??= (await _client.GetQueueUrlAsync(_queueName, cancellationToken)).QueueUrl;

        await _client.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = jobId.ToString(),
        }, cancellationToken);
    }

    private static AmazonSQSClient CreateClient(string endpoint)
    {
        var config = new AmazonSQSConfig
        {
            ServiceURL = endpoint,
        };

        return IsLocalEndpoint(endpoint)
            ? new AmazonSQSClient(new BasicAWSCredentials("local", "local"), config)
            : new AmazonSQSClient(config);
    }

    private static bool IsLocalEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback ||
            string.Equals(uri.Host, "elasticmq", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "host.docker.internal", StringComparison.OrdinalIgnoreCase);
    }
}
