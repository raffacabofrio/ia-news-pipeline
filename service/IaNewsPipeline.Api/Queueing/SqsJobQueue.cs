using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace IaNewsPipeline.Api.Queueing;

public sealed class SqsJobQueue : IJobQueue
{
    private const int LongPollSeconds = 20;
    private const int VisibilityTimeoutSeconds = 120;

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

    public async Task<IReadOnlyList<QueuedJobMessage>> ReceiveAsync(CancellationToken cancellationToken)
    {
        _queueUrl ??= (await _client.GetQueueUrlAsync(_queueName, cancellationToken)).QueueUrl;

        var response = await _client.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 5,
            WaitTimeSeconds = LongPollSeconds,
            VisibilityTimeout = VisibilityTimeoutSeconds,
        }, cancellationToken);

        return response.Messages
            .Select(message => new QueuedJobMessage(message.ReceiptHandle, message.Body))
            .ToArray();
    }

    public async Task DeleteAsync(string receiptHandle, CancellationToken cancellationToken)
    {
        _queueUrl ??= (await _client.GetQueueUrlAsync(_queueName, cancellationToken)).QueueUrl;

        await _client.DeleteMessageAsync(_queueUrl, receiptHandle, cancellationToken);
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
