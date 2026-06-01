using PatternKit.Behavioral.NullObject;
using PatternKit.Generators.NullObject;

namespace PatternKit.Examples.NullObjectDemo;

[GenerateNullObject(TypeName = "NullCustomerNotificationChannel")]
public interface ICustomerNotificationChannel
{
    string Name { get; }

    [NullObjectDefault(false)]
    bool CanDeliver { get; }

    [NullObjectDefault("suppressed")]
    string Send(CustomerNotification notification);
}

public sealed record CustomerNotification(string CustomerId, string Subject, string Body);

public sealed record CustomerNotificationResult(string Channel, string Status, bool Delivered);

public sealed class CustomerNotificationWorkflow(ICustomerNotificationChannel channel)
{
    public CustomerNotificationResult Notify(CustomerNotification notification)
    {
        if (notification is null)
            throw new ArgumentNullException(nameof(notification));

        var status = channel.Send(notification);
        return new CustomerNotificationResult(channel.Name, status, channel.CanDeliver);
    }
}

public static class CustomerNotificationNullObjectDemo
{
    public static NullObject<ICustomerNotificationChannel> CreateFluentFallback()
        => NullObject<ICustomerNotificationChannel>
            .Create(NullCustomerNotificationChannel.Instance)
            .Build();
}
