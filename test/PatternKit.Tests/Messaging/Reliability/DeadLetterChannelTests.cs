using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class DeadLetterChannelTests
{
    [Scenario("CaptureAsync StoresFailedMessageWithReasonAttemptsAndHeaders")]
    [Fact]
    public async Task CaptureAsync_StoresFailedMessageWithReasonAttemptsAndHeaders()
    {
        var store = new InMemoryDeadLetterStore<Order>();
        var failedAt = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var channel = DeadLetterChannel<Order>.Create("checkout-dead")
            .FromSource("checkout.fulfillment")
            .UseStore(store)
            .UseClock(() => failedAt)
            .Build();

        var deadLetter = await channel.CaptureAsync(
            Message<Order>.Create(new Order("order-1")).WithMessageId("msg-1").WithCorrelationId("corr-1"),
            "fulfillment unavailable",
            new InvalidOperationException("gateway down"),
            attempts: 3);

        ScenarioExpect.Equal("dead:msg-1", deadLetter.Id);
        ScenarioExpect.Equal("fulfillment unavailable", deadLetter.Reason);
        ScenarioExpect.Equal(3, deadLetter.Attempts);
        ScenarioExpect.Equal(failedAt, deadLetter.FailedAt);
        ScenarioExpect.Equal("checkout.fulfillment", deadLetter.Source);
        ScenarioExpect.Equal(typeof(InvalidOperationException).FullName, deadLetter.ExceptionType);
        ScenarioExpect.Equal("gateway down", deadLetter.ExceptionMessage);
        ScenarioExpect.Equal("corr-1", deadLetter.Message.Headers.CorrelationId);
        ScenarioExpect.Equal("checkout-dead", deadLetter.Message.Headers.GetString("dead-letter-channel"));
        ScenarioExpect.Single(store.Messages);
    }

    [Scenario("PrepareReplayAsync ReturnsReplayMessageWithReplayMetadata")]
    [Fact]
    public async Task PrepareReplayAsync_ReturnsReplayMessageWithReplayMetadata()
    {
        var store = new InMemoryDeadLetterStore<Order>();
        var channel = DeadLetterChannel<Order>.Create()
            .UseStore(store)
            .UseIds(static (message, _, _) => "dlq:" + message.Payload.Id)
            .UseClock(() => new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero))
            .Build();
        var deadLetter = await channel.CaptureAsync(Message<Order>.Create(new Order("order-1")), "poison message");

        var replay = await channel.PrepareReplayAsync(deadLetter.Id);

        ScenarioExpect.True(replay.Found);
        ScenarioExpect.True(replay.ReadyForReplay);
        ScenarioExpect.Equal(deadLetter, replay.DeadLetter);
        ScenarioExpect.Equal("order-1", replay.Message!.Payload.Id);
        ScenarioExpect.Equal("dlq:order-1", replay.Message.Headers.GetString("dead-letter-replayed-from"));
        ScenarioExpect.NotNull(replay.Message.Headers.GetString("dead-letter-replay-id"));
    }

    [Scenario("PrepareReplayAsync ReturnsMissForUnknownDeadLetter")]
    [Fact]
    public async Task PrepareReplayAsync_ReturnsMissForUnknownDeadLetter()
    {
        var channel = DeadLetterChannel<Order>.Create().Build();

        var replay = await channel.PrepareReplayAsync("missing");

        ScenarioExpect.False(replay.Found);
        ScenarioExpect.False(replay.ReadyForReplay);
        ScenarioExpect.Equal("Dead-letter message was not found.", replay.MissingReason);
    }

    [Scenario("DeadLetterChannel ValidatesConfigurationAndInputs")]
    [Fact]
    public async Task DeadLetterChannel_ValidatesConfigurationAndInputs()
    {
        ScenarioExpect.Throws<ArgumentException>(() => DeadLetterChannel<Order>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() => DeadLetterChannel<Order>.Create().FromSource(" "));
        ScenarioExpect.Throws<ArgumentNullException>(() => DeadLetterChannel<Order>.Create().UseStore(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => DeadLetterChannel<Order>.Create().UseIds(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => DeadLetterChannel<Order>.Create().UseClock(null!));

        var channel = DeadLetterChannel<Order>.Create().Build();
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await channel.CaptureAsync(null!, "failed"));
        await ScenarioExpect.ThrowsAsync<ArgumentException>(async () => await channel.CaptureAsync(Message<Order>.Create(new Order("1")), ""));
        await ScenarioExpect.ThrowsAsync<ArgumentOutOfRangeException>(async () => await channel.CaptureAsync(Message<Order>.Create(new Order("1")), "failed", attempts: -1));
        await ScenarioExpect.ThrowsAsync<ArgumentException>(async () => await channel.PrepareReplayAsync(""));
    }

    [Scenario("InMemoryDeadLetterStore ObservesCancellationAndValidation")]
    [Fact]
    public async Task InMemoryDeadLetterStore_ObservesCancellationAndValidation()
    {
        var store = new InMemoryDeadLetterStore<Order>();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await store.EnqueueAsync(null!));
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () =>
            await store.EnqueueAsync(new DeadLetterMessage<Order>("id", Message<Order>.Create(new Order("1")), "failed", DateTimeOffset.UtcNow, 0), source.Token));
        await ScenarioExpect.ThrowsAsync<ArgumentException>(async () => await store.TryLoadAsync(""));
    }

    private sealed record Order(string Id);
}
