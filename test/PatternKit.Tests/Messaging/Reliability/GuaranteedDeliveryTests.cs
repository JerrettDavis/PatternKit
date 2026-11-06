using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class GuaranteedDeliveryTests
{
    [Scenario("EnqueueAsync StoresPendingRecord")]
    [Fact]
    public async Task EnqueueAsync_StoresPendingRecord()
    {
        var store = new InMemoryGuaranteedDeliveryStore<Order>();
        var queue = GuaranteedDeliveryQueue<Order>.Create(store)
            .Name("orders")
            .LeaseDuration(TimeSpan.FromMinutes(2))
            .MaxDeliveryAttempts(3)
            .Build();
        var createdAt = new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

        var record = await queue.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "delivery-1", createdAt);

        ScenarioExpect.Equal("delivery-1", record.Id);
        ScenarioExpect.Equal("order-1", record.Message.Payload.Id);
        ScenarioExpect.Equal(createdAt, record.EnqueuedAt);
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Pending, record.Status);
        ScenarioExpect.Single(store.Records);
    }

    [Scenario("TryReceiveAsync LeasesNextPendingRecord")]
    [Fact]
    public async Task TryReceiveAsync_LeasesNextPendingRecord()
    {
        var now = new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);
        var store = new InMemoryGuaranteedDeliveryStore<Order>();
        var queue = GuaranteedDeliveryQueue<Order>.Create(store)
            .LeaseDuration(TimeSpan.FromMinutes(1))
            .Clock(() => now)
            .Build();
        await queue.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "delivery-1");

        var lease = await queue.TryReceiveAsync();
        var second = await queue.TryReceiveAsync();

        ScenarioExpect.NotNull(lease);
        ScenarioExpect.Equal("delivery-1", lease!.Id);
        ScenarioExpect.Equal("order-1", lease.Message.Payload.Id);
        ScenarioExpect.Null(second);
        var record = ScenarioExpect.Single(await queue.SnapshotAsync());
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Leased, record.Status);
        ScenarioExpect.Equal(1, record.Attempts);
        ScenarioExpect.Equal(now.AddMinutes(1), record.LeasedUntil);
    }

    [Scenario("AcknowledgeAsync MarksLeasedRecordDelivered")]
    [Fact]
    public async Task AcknowledgeAsync_MarksLeasedRecordDelivered()
    {
        var store = new InMemoryGuaranteedDeliveryStore<Order>();
        var queue = GuaranteedDeliveryQueue<Order>.Create(store).Build();
        await queue.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "delivery-1");
        var lease = await queue.TryReceiveAsync();

        await queue.AcknowledgeAsync(lease!);

        var record = ScenarioExpect.Single(await queue.SnapshotAsync());
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Delivered, record.Status);
        ScenarioExpect.Null(record.LeasedUntil);
    }

    [Scenario("ReleaseAsync MakesRecordReceivableAgain")]
    [Fact]
    public async Task ReleaseAsync_MakesRecordReceivableAgain()
    {
        var store = new InMemoryGuaranteedDeliveryStore<Order>();
        var queue = GuaranteedDeliveryQueue<Order>.Create(store).Build();
        await queue.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "delivery-1");
        var lease = await queue.TryReceiveAsync();

        await queue.ReleaseAsync(lease!, "transient failure");
        var retry = await queue.TryReceiveAsync();

        ScenarioExpect.NotNull(retry);
        ScenarioExpect.Equal("delivery-1", retry!.Id);
        var record = ScenarioExpect.Single(await queue.SnapshotAsync());
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Leased, record.Status);
        ScenarioExpect.Equal(2, record.Attempts);
    }

    [Scenario("TryReceiveAsync DeadLettersExceededAttempts")]
    [Fact]
    public async Task TryReceiveAsync_DeadLettersExceededAttempts()
    {
        var now = new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);
        var store = new InMemoryGuaranteedDeliveryStore<Order>();
        var queue = GuaranteedDeliveryQueue<Order>.Create(store)
            .MaxDeliveryAttempts(1)
            .LeaseDuration(TimeSpan.FromMilliseconds(1))
            .Clock(() => now)
            .Build();
        await queue.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "delivery-1");
        _ = await queue.TryReceiveAsync();

        now = now.AddMinutes(1);
        var retry = await queue.TryReceiveAsync();

        ScenarioExpect.Null(retry);
        var record = ScenarioExpect.Single(await queue.SnapshotAsync());
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.DeadLettered, record.Status);
        ScenarioExpect.Equal("Maximum delivery attempts exceeded.", record.LastError);
    }

    [Scenario("DeadLetterAsync MarksRecordDeadLettered")]
    [Fact]
    public async Task DeadLetterAsync_MarksRecordDeadLettered()
    {
        var queue = GuaranteedDeliveryQueue<Order>.Create(new InMemoryGuaranteedDeliveryStore<Order>()).Build();
        await queue.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "delivery-1");
        var lease = await queue.TryReceiveAsync();

        await queue.DeadLetterAsync(lease!, "poison message");

        var record = ScenarioExpect.Single(await queue.SnapshotAsync());
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.DeadLettered, record.Status);
        ScenarioExpect.Equal("poison message", record.LastError);
    }

    [Scenario("GuaranteedDelivery ValidatesArguments")]
    [Fact]
    public async Task GuaranteedDelivery_ValidatesArguments()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => GuaranteedDeliveryQueue<Order>.Create(null!));
        ScenarioExpect.Throws<ArgumentException>(() => GuaranteedDeliveryQueue<Order>.Create(new InMemoryGuaranteedDeliveryStore<Order>()).Name(""));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => GuaranteedDeliveryQueue<Order>.Create(new InMemoryGuaranteedDeliveryStore<Order>()).LeaseDuration(TimeSpan.Zero));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => GuaranteedDeliveryQueue<Order>.Create(new InMemoryGuaranteedDeliveryStore<Order>()).MaxDeliveryAttempts(0));
        ScenarioExpect.Throws<ArgumentNullException>(() => GuaranteedDeliveryQueue<Order>.Create(new InMemoryGuaranteedDeliveryStore<Order>()).Clock(null!));

        var queue = GuaranteedDeliveryQueue<Order>.Create(new InMemoryGuaranteedDeliveryStore<Order>()).Build();
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await queue.EnqueueAsync(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await queue.AcknowledgeAsync(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await queue.ReleaseAsync(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await queue.DeadLetterAsync(null!));
    }

    private sealed record Order(string Id);
}
