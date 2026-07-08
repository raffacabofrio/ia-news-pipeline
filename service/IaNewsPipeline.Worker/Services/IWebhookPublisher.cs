namespace IaNewsPipeline.Worker.Services;

public interface IWebhookPublisher
{
    Task<PublishResult> PublishAsync(WebhookPublishRequest request, CancellationToken cancellationToken);
}
