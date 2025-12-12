using Microsoft.Extensions.Logging;

namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public sealed class QueueNotificationPublisher(ILogger<QueueNotificationPublisher> logger) : INotificationPublisher
{
    public Task PublishAsync(string topic, string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Published {Topic}: {Message}", topic, message);
        return Task.CompletedTask;
    }
}