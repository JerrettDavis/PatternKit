using PatternKit.Examples.ObserverGeneratorDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ObserverGeneratorDemo;

[Feature("Observer generator demos")]
[Collection(PatternKit.Examples.Tests.ConsoleTestCollection.Name)]
public sealed class ObserverGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
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

    [Scenario("Temperature monitor publishes readings, alerts, subscriber failures, and lifecycle disposal")]
    [Fact]
    public async Task TemperatureDemos_RunThroughPublicDemoWorkflows()
    {
        await Given("a redirected console", CaptureConsole)
            .When("running the temperature demos", string (capture) =>
            {
                try
                {
                    TemperatureMonitorDemo.Run();
                    MultipleSubscribersDemo.Run();
                    SubscriptionLifecycleDemo.Run();
                    return capture.Output();
                }
                finally
                {
                    capture.Dispose();
                }
            })
            .Then("sensor readings were published", output => output.Contains("Simulating sensor readings", StringComparison.Ordinal))
            .And("subscriber failures were isolated", output => output.Contains("Subscriber 3 threw exceptions", StringComparison.Ordinal))
            .And("disposed subscriptions stopped receiving readings", output => output.Contains("After 'using' block", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Async notification demos cover channel fan-out, mixed handlers, and cancellation")]
    [Fact]
    public async Task NotificationDemos_RunThroughPublicDemoWorkflows()
    {
        await Given("a redirected console", CaptureConsole)
            .When("running the notification demos", async Task<string> (capture) =>
            {
                try
                {
                    await AsyncNotificationDemo.RunAsync();
                    ExceptionHandlingDemo.Run();
                    await MixedHandlersDemo.RunAsync();
                    await CancellationDemo.RunAsync();
                    return capture.Output();
                }
                finally
                {
                    capture.Dispose();
                }
            })
            .Then("async notification fan-out completed", output => output.Contains("Results:", StringComparison.Ordinal))
            .And("aggregate policy demo attempted every validator", output => output.Contains("Validator 3: Success", StringComparison.Ordinal))
            .And("mixed sync and async handlers were awaited", output => output.Contains("PublishAsync waits", StringComparison.Ordinal))
            .And("cancellation was observed", output => output.Contains("PublishAsync was cancelled", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Notification system returns channel-specific happy and sad results")]
    [Fact]
    public async Task NotificationSystem_ReturnsChannelSpecificResults()
    {
        await Given("a notification system", () => new NotificationSystem())
            .When("sending low and high priority notifications through direct channel APIs",
                async Task<(NotificationResult lowSms, NotificationResult highSms, NotificationResult push)> (system) =>
            {
                var low = new Notification("user-low", "digest", 0);
                var high = new Notification("user-high", "security alert", 2);

                var lowSms = await system.SendSmsAsync(low);
                var highSms = await system.SendSmsAsync(high);
                var push = await system.SendPushAsync(low);

                return (lowSms, highSms, push);
            })
            .Then("low priority SMS is rejected", result =>
                !result.lowSms.Success
                && result.lowSms.Channel == "SMS"
                && result.lowSms.Error == "Priority too low for SMS")
            .And("high priority SMS succeeds", result => result.highSms is { Success: true, Channel: "SMS" })
            .And("push succeeds for normal notifications", result => result.push is { Success: true, Channel: "Push" })
            .AssertPassed();
    }

    private static ConsoleCapture CaptureConsole() => new();

    private sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _original = Console.Out;
        private readonly StringWriter _writer = new();

        public ConsoleCapture()
        {
            Console.SetOut(_writer);
        }

        public string Output() => _writer.ToString();

        public void Dispose()
        {
            Console.SetOut(_original);
            _writer.Dispose();
        }
    }
}
