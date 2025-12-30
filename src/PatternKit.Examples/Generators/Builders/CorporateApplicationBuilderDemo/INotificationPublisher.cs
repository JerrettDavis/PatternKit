namespace PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

public interface INotificationPublisher
{
    Task PublishAsync(string topic, string message, CancellationToken cancellationToken = default);
}