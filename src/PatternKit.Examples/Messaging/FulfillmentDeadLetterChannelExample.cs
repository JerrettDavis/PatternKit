using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates fluent and source-generated dead-letter channels for failed fulfillment messages.
/// </summary>
public static class FulfillmentDeadLetterChannelExample
{
    /// <summary>Runs the fluent dead-letter path.</summary>
    public static FulfillmentDeadLetterSummary RunFluent()
    {
        var store = new InMemoryDeadLetterStore<FulfillmentCommand>();
        var channel = FulfillmentDeadLetterPolicies.CreateFluentChannel(store);
        return CaptureAndReplay(channel, CreateCommand("order-100"), "carrier timeout");
    }

    /// <summary>Runs the source-generated dead-letter path.</summary>
    public static FulfillmentDeadLetterSummary RunGenerated()
        => CaptureAndReplay(
            GeneratedFulfillmentDeadLetters.CreateChannel(),
            CreateCommand("order-200"),
            "warehouse rejected request");

    /// <summary>Creates a production-shaped fulfillment command envelope.</summary>
    public static Message<FulfillmentCommand> CreateCommand(string orderId)
        => Message<FulfillmentCommand>.Create(new FulfillmentCommand(orderId, "warehouse-east"))
            .WithMessageId("fulfillment:" + orderId)
            .WithCorrelationId("checkout:" + orderId)
            .WithContentType("application/vnd.patternkit.fulfillment+json");

    internal static FulfillmentDeadLetterSummary CaptureAndReplay(
        DeadLetterChannel<FulfillmentCommand> channel,
        Message<FulfillmentCommand> command,
        string reason)
    {
        var captured = channel.Capture(command, reason, new TimeoutException(reason), attempts: 4);
        var replay = channel.PrepareReplayAsync(captured.Id).AsTask().GetAwaiter().GetResult();

        return new FulfillmentDeadLetterSummary(
            captured.Id,
            captured.Reason,
            captured.Attempts,
            captured.Message.Headers.CorrelationId ?? string.Empty,
            replay.ReadyForReplay,
            replay.Message?.Headers.GetString("dead-letter-replayed-from") ?? string.Empty);
    }
}

/// <summary>Fulfillment command that would normally be delivered to a warehouse or carrier boundary.</summary>
public sealed record FulfillmentCommand(string OrderId, string Warehouse);

/// <summary>Summary returned by the dead-letter channel example.</summary>
public sealed record FulfillmentDeadLetterSummary(
    string DeadLetterId,
    string Reason,
    int Attempts,
    string CorrelationId,
    bool ReadyForReplay,
    string ReplayedFrom);

/// <summary>Fluent dead-letter channel policy helpers.</summary>
public static class FulfillmentDeadLetterPolicies
{
    public static DeadLetterChannel<FulfillmentCommand> CreateFluentChannel(
        IDeadLetterStore<FulfillmentCommand> store)
        => DeadLetterChannel<FulfillmentCommand>.Create("fulfillment-dead-letter")
            .FromSource("checkout.fulfillment")
            .UseStore(store)
            .UseIds(static (message, _, _) => "fulfillment-dead:" + (message.Headers.MessageId ?? message.Payload.OrderId))
            .Build();
}

/// <summary>DI-friendly service that imports the dead-letter workflow into an application.</summary>
public sealed class FulfillmentDeadLetterWorkflow
{
    private readonly DeadLetterChannel<FulfillmentCommand> _channel;

    public FulfillmentDeadLetterWorkflow(DeadLetterChannel<FulfillmentCommand> channel)
    {
        _channel = channel;
    }

    public FulfillmentDeadLetterSummary Capture(Message<FulfillmentCommand> command, string reason)
        => FulfillmentDeadLetterChannelExample.CaptureAndReplay(_channel, command, reason);
}

/// <summary>Runner exposing fluent and generated dead-letter channel paths.</summary>
public sealed record FulfillmentDeadLetterChannelExampleRunner(
    Func<FulfillmentDeadLetterSummary> RunFluent,
    Func<FulfillmentDeadLetterSummary> RunGenerated);

/// <summary>Dependency injection extensions for the dead-letter channel example.</summary>
public static class FulfillmentDeadLetterChannelServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentDeadLetterChannelExample(this IServiceCollection services)
    {
        services.AddSingleton<IDeadLetterStore<FulfillmentCommand>, InMemoryDeadLetterStore<FulfillmentCommand>>();
        services.AddSingleton(static sp => FulfillmentDeadLetterPolicies.CreateFluentChannel(
            sp.GetRequiredService<IDeadLetterStore<FulfillmentCommand>>()));
        services.AddSingleton<FulfillmentDeadLetterWorkflow>();
        services.AddSingleton(new FulfillmentDeadLetterChannelExampleRunner(
            FulfillmentDeadLetterChannelExample.RunFluent,
            FulfillmentDeadLetterChannelExample.RunGenerated));
        return services;
    }
}

/// <summary>Source-generated dead-letter channel used by the production-shaped example.</summary>
[GenerateDeadLetterChannel(
    typeof(FulfillmentCommand),
    FactoryName = "CreateChannel",
    ChannelName = "fulfillment-dead-letter",
    Source = "checkout.fulfillment",
    IdPrefix = "fulfillment-dead")]
public static partial class GeneratedFulfillmentDeadLetters
{
    [DeadLetterStoreFactory]
    private static IDeadLetterStore<FulfillmentCommand> CreateStore()
        => new InMemoryDeadLetterStore<FulfillmentCommand>();
}
