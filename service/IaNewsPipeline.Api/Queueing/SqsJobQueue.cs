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
        _client = new AmazonSQSClient(
            new BasicAWSCredentials("local", "local"),
            new AmazonSQSConfig
            {
                ServiceURL = endpoint,
            });
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
}
