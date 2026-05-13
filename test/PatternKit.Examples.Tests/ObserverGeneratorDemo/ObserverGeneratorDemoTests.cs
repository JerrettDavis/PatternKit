using PatternKit.Examples.ObserverGeneratorDemo;

namespace PatternKit.Examples.Tests.ObserverGeneratorDemo;

public sealed class ObserverGeneratorDemoTests
{
    [Fact]
    public void TemperatureChanged_PublishesToSubscribersInOrder()
    {
        var changed = new TemperatureChanged();
        var received = new List<string>();

        using var first = changed.Subscribe(reading => received.Add($"first:{reading.SensorId}:{reading.Celsius:F1}"));
        using var second = changed.Subscribe(reading => received.Add($"second:{reading.SensorId}:{reading.Celsius:F1}"));

        changed.Publish(new TemperatureReading("sensor-a", 21.5, DateTime.UtcNow));

        Assert.Equal(["first:sensor-a:21.5", "second:sensor-a:21.5"], received);
    }

    [Fact]
    public void TemperatureChanged_DisposedSubscription_IsNotInvoked()
    {
        var changed = new TemperatureChanged();
        var count = 0;
        var subscription = changed.Subscribe(_ => count++);

        changed.Publish(new TemperatureReading("sensor-a", 20, DateTime.UtcNow));
        subscription.Dispose();
        changed.Publish(new TemperatureReading("sensor-a", 21, DateTime.UtcNow));

        Assert.Equal(1, count);
    }

    [Fact]
    public void TemperatureAlertRaised_StopPolicy_DoesNotStopSubsequentGeneratedSubscribers()
    {
        var alertRaised = new TemperatureAlertRaised();
        var handled = false;
        using var failing = alertRaised.Subscribe(_ => throw new InvalidOperationException("halt"));
        using var succeeding = alertRaised.Subscribe(_ => handled = true);

        alertRaised.Publish(new TemperatureAlert("sensor-a", 30, 25));

        Assert.True(handled);
    }

    [Fact]
    public async Task NotificationPublished_AwaitsAsyncSubscribers()
    {
        var published = new NotificationPublished();
        var received = new List<string>();

        using var _ = published.Subscribe(async notification =>
        {
            await Task.Yield();
            received.Add(notification.RecipientId);
        });

        await published.PublishAsync(new Notification("user-1", "hello", 1));

        Assert.Equal(["user-1"], received);
    }

    [Fact]
    public void NotificationSent_ContinuesAfterSubscriberExceptions()
    {
        var sent = new NotificationSent();
        var handled = false;
        using var first = sent.Subscribe(_ => throw new InvalidOperationException("first"));
        using var second = sent.Subscribe(_ => throw new ArgumentException("second"));
        using var third = sent.Subscribe(_ => handled = true);

        sent.Publish(new NotificationResult(true, "Email"));

        Assert.True(handled);
    }

    [Fact]
    public async Task NotificationSystem_SendsThroughRegisteredAsyncHandlers()
    {
        var system = new NotificationSystem();
        var results = new List<NotificationResult>();

        using var resultSubscription = system.OnNotificationSent(results.Add);
        using var pushSubscription = system.SubscribeAsync(async notification =>
        {
            var result = await system.SendPushAsync(notification);
            system.ReportSent(result);
        });

        await system.SendAsync(new Notification("user-1", "hello", 1));

        var result = Assert.Single(results);
        Assert.True(result.Success);
        Assert.Equal("Push", result.Channel);
    }
}
