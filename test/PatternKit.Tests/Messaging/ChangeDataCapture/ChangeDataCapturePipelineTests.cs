using PatternKit.Messaging.ChangeDataCapture;
using TinyBDD;

namespace PatternKit.Tests.Messaging.ChangeDataCapture;

public sealed class ChangeDataCapturePipelineTests
{
    [Scenario("Change Data Capture exposes mutation entry and summary value contracts")]
    [Fact]
    public void Change_Data_Capture_Exposes_Mutation_Entry_And_Summary_Value_Contracts()
    {
        var occurredAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var mutation = new ChangeDataCaptureMutation<string, InventoryMutation>(
            "sku-1",
            ChangeDataCaptureOperation.Upsert,
            "InventoryItem",
            new("sku-1", 5),
            42,
            occurredAt);
        var entry = new ChangeDataCaptureEntry<ChangeDataCaptureMutation<string, InventoryMutation>, InventoryChanged>(
            7,
            "inventory-cdc",
            mutation,
            new(7, "sku-1", 5),
            occurredAt);
        var published = entry.MarkPublished(occurredAt.AddMinutes(1));
        var attempted = entry.MarkAttempt();
        var summary = new ChangeDataCapturePublishSummary(1, 0);

        ScenarioExpect.Equal("sku-1", mutation.Key);
        ScenarioExpect.Equal(ChangeDataCaptureOperation.Upsert, mutation.Operation);
        ScenarioExpect.Equal("InventoryItem", mutation.EntityName);
        ScenarioExpect.Equal(42, mutation.Version);
        ScenarioExpect.Equal(occurredAt, mutation.OccurredAt);
        ScenarioExpect.Equal("sku-1", mutation.Payload.Sku);
        ScenarioExpect.Equal(7, entry.Sequence);
        ScenarioExpect.Equal("inventory-cdc", entry.PipelineName);
        ScenarioExpect.Same(mutation, entry.Mutation);
        ScenarioExpect.Equal("sku-1", entry.Event.Sku);
        ScenarioExpect.Equal(occurredAt, entry.CapturedAt);
        ScenarioExpect.True(published.Published);
        ScenarioExpect.Equal(occurredAt.AddMinutes(1), published.PublishedAt);
        ScenarioExpect.Equal(1, published.Attempts);
        ScenarioExpect.Equal(1, attempted.Attempts);
        ScenarioExpect.Equal(1, summary.Published);
        ScenarioExpect.Equal(0, summary.Failed);
        ScenarioExpect.True(summary.Equals(new ChangeDataCapturePublishSummary(1, 0)));
        ScenarioExpect.True(summary.Equals((object)new ChangeDataCapturePublishSummary(1, 0)));
        ScenarioExpect.True(summary == new ChangeDataCapturePublishSummary(1, 0));
        ScenarioExpect.True(summary != new ChangeDataCapturePublishSummary(0, 1));
        ScenarioExpect.NotEqual(0, summary.GetHashCode());
        ScenarioExpect.Throws<ArgumentException>(() => new ChangeDataCaptureMutation<string, InventoryMutation>("sku", ChangeDataCaptureOperation.Insert, " ", new("sku", 1), 1, occurredAt));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => new ChangeDataCaptureEntry<InventoryMutation, InventoryChanged>(0, "inventory", new("sku", 1), new(1, "sku", 1), occurredAt));
        ScenarioExpect.Throws<ArgumentException>(() => new ChangeDataCaptureEntry<InventoryMutation, InventoryChanged>(1, "", new("sku", 1), new(1, "sku", 1), occurredAt));
    }

    [Scenario("Change Data Capture captures and publishes mutations in order")]
    [Fact]
    public async Task Change_Data_Capture_Captures_And_Publishes_Mutations_In_Order()
    {
        var published = new List<InventoryChanged>();
        var store = new InMemoryChangeDataCaptureStore<InventoryMutation, InventoryChanged>();
        var pipeline = ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create("inventory-cdc")
            .UseStore(store)
            .MapWith(static (mutation, sequence) => new InventoryChanged(sequence, mutation.Sku, mutation.Quantity))
            .PublishWith((@event, _) =>
            {
                published.Add(@event);
                return default;
            })
            .Build();

        var first = await pipeline.CaptureAsync(new("sku-1", 5));
        var second = await pipeline.CaptureAsync(new("sku-2", 9));
        var summary = await pipeline.PublishPendingAsync();

        ScenarioExpect.Equal(1, first.Sequence);
        ScenarioExpect.Equal(2, second.Sequence);
        ScenarioExpect.Equal([1L, 2L], published.Select(static e => e.Sequence).ToArray());
        ScenarioExpect.Equal(new ChangeDataCapturePublishSummary(2, 0), summary);
        ScenarioExpect.True(store.Snapshot().All(static entry => entry.Published));
    }

    [Scenario("Change Data Capture leaves failed publications pending")]
    [Fact]
    public async Task Change_Data_Capture_Leaves_Failed_Publications_Pending()
    {
        var pipeline = ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create("inventory-cdc")
            .MapWith(static (mutation, sequence) => new InventoryChanged(sequence, mutation.Sku, mutation.Quantity))
            .PublishWith((_, _) => throw new InvalidOperationException("broker unavailable"))
            .Build();

        await pipeline.CaptureAsync(new("sku-1", 5));
        var summary = await pipeline.PublishPendingAsync();
        var pending = await pipeline.ReadPendingAsync();

        ScenarioExpect.Equal(new ChangeDataCapturePublishSummary(0, 1), summary);
        var entry = ScenarioExpect.Single(pending);
        ScenarioExpect.False(entry.Published);
        ScenarioExpect.Equal(1, entry.Attempts);
    }

    [Scenario("Change Data Capture validates configuration and store operations")]
    [Fact]
    public async Task Change_Data_Capture_Validates_Configuration_And_Store_Operations()
    {
        ScenarioExpect.Throws<ArgumentException>(() => ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create("").MapWith(Map).PublishWith(Publish).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create().MapWith(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create().PublishWith(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create().UseStore(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create().WithClock(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create().PublishWith(Publish).Build());
        ScenarioExpect.Throws<InvalidOperationException>(() => ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create().MapWith(Map).Build());

        var store = new InMemoryChangeDataCaptureStore<InventoryMutation, InventoryChanged>();
        await ScenarioExpect.ThrowsAsync<ArgumentException>(() => store.GetNextSequenceAsync("").AsTask());
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => store.GetNextSequenceAsync("inventory", new CancellationToken(true)).AsTask());
        await ScenarioExpect.ThrowsAsync<ArgumentException>(() => store.AppendAsync("", 1, new("sku", 1), new(1, "sku", 1), DateTimeOffset.UtcNow).AsTask());
        await ScenarioExpect.ThrowsAsync<ArgumentException>(() => store.ReadPendingAsync(" ").AsTask());
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => store.ReadPendingAsync("inventory", new CancellationToken(true)).AsTask());
        await ScenarioExpect.ThrowsAsync<ArgumentOutOfRangeException>(() => store.AppendAsync("inventory", 0, new("sku", 1), new(1, "sku", 1), DateTimeOffset.UtcNow).AsTask());
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => store.AppendAsync("inventory", 1, new("sku", 1), new(1, "sku", 1), DateTimeOffset.UtcNow, new CancellationToken(true)).AsTask());
        _ = await store.AppendAsync("inventory", 1, new("sku", 1), new(1, "sku", 1), DateTimeOffset.UtcNow);
        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() => store.AppendAsync("inventory", 1, new("sku", 1), new(1, "sku", 1), DateTimeOffset.UtcNow).AsTask());
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => store.MarkPublishedAsync(1, DateTimeOffset.UtcNow, new CancellationToken(true)).AsTask());
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => store.MarkAttemptAsync(1, new CancellationToken(true)).AsTask());
        await ScenarioExpect.ThrowsAsync<KeyNotFoundException>(() => store.MarkAttemptAsync(404).AsTask());
        await store.MarkPublishedAsync(1, DateTimeOffset.UtcNow);

        static InventoryChanged Map(InventoryMutation mutation, long sequence) => new(sequence, mutation.Sku, mutation.Quantity);
        static ValueTask Publish(InventoryChanged _, CancellationToken __) => default;
    }

    [Scenario("Change Data Capture propagates publisher cancellation")]
    [Fact]
    public async Task Change_Data_Capture_Propagates_Publisher_Cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var pipeline = ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create("inventory-cdc")
            .MapWith(static (mutation, sequence) => new InventoryChanged(sequence, mutation.Sku, mutation.Quantity))
            .PublishWith((_, token) =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(token);
            })
            .Build();

        await pipeline.CaptureAsync(new("sku-1", 5));

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => pipeline.PublishPendingAsync(cancellation.Token).AsTask());
        var pending = await pipeline.ReadPendingAsync();
        var entry = ScenarioExpect.Single(pending);
        ScenarioExpect.Equal(0, entry.Attempts);
    }

    [Scenario("Change Data Capture validates capture arguments and custom clock")]
    [Fact]
    public async Task Change_Data_Capture_Validates_Capture_Arguments_And_Custom_Clock()
    {
        var capturedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var pipeline = ChangeDataCapturePipeline<InventoryMutation, InventoryChanged>.Create("inventory-cdc")
            .MapWith(static (mutation, sequence) => new InventoryChanged(sequence, mutation.Sku, mutation.Quantity))
            .PublishWith(static (_, _) => default)
            .WithClock(() => capturedAt)
            .Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => pipeline.CaptureAsync(null!).AsTask());
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => pipeline.CaptureAsync(new("sku-1", 5), new CancellationToken(true)).AsTask());
        var entry = await pipeline.CaptureAsync(new("sku-1", 5));

        ScenarioExpect.Equal("inventory-cdc", pipeline.Name);
        ScenarioExpect.Equal(capturedAt, entry.CapturedAt);
    }

    private sealed record InventoryMutation(string Sku, int Quantity);
    private sealed record InventoryChanged(long Sequence, string Sku, int Quantity);
}
