using PatternKit.Messaging.Transformation;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Transformation;

public sealed class KeyedNormalizerTests
{
    // ─── Happy path ───────────────────────────────────────────────────────────

    [Scenario("NormalizeAsync RegisteredKey DispatchesToCorrectHandler")]
    [Fact]
    public async Task NormalizeAsync_RegisteredKey_DispatchesToCorrectHandler()
    {
        var normalizer = KeyedNormalizer<string, string, Order>.Create()
            .When("json", (raw, _) => new ValueTask<Order>(new Order(raw, "json")))
            .When("xml", (raw, _) => new ValueTask<Order>(new Order(raw, "xml")))
            .Build();

        var result = await normalizer.NormalizeAsync("json", "payload-1");

        ScenarioExpect.Equal("json", result.Format);
        ScenarioExpect.Equal("payload-1", result.Id);
    }

    [Scenario("NormalizeAsync MultipleKeys EachRoutesCorrectly")]
    [Fact]
    public async Task NormalizeAsync_MultipleKeys_EachRoutesCorrectly()
    {
        var normalizer = KeyedNormalizer<string, string, Order>.Create()
            .When("csv", (raw, _) => new ValueTask<Order>(new Order(raw, "csv")))
            .When("avro", (raw, _) => new ValueTask<Order>(new Order(raw, "avro")))
            .When("proto", (raw, _) => new ValueTask<Order>(new Order(raw, "proto")))
            .Build();

        var csv = await normalizer.NormalizeAsync("csv", "c");
        var avro = await normalizer.NormalizeAsync("avro", "a");
        var proto = await normalizer.NormalizeAsync("proto", "p");

        ScenarioExpect.Equal("csv", csv.Format);
        ScenarioExpect.Equal("avro", avro.Format);
        ScenarioExpect.Equal("proto", proto.Format);
    }

    // ─── Default handler ──────────────────────────────────────────────────────

    [Scenario("NormalizeAsync UnregisteredKeyWithDefault DispatchesToDefault")]
    [Fact]
    public async Task NormalizeAsync_UnregisteredKeyWithDefault_DispatchesToDefault()
    {
        var normalizer = KeyedNormalizer<string, string, Order>.Create()
            .When("json", (raw, _) => new ValueTask<Order>(new Order(raw, "json")))
            .Default((raw, _) => new ValueTask<Order>(new Order(raw, "unknown")))
            .Build();

        var result = await normalizer.NormalizeAsync("csv", "anything");

        ScenarioExpect.Equal("unknown", result.Format);
    }

    // ─── Missing key, no default ──────────────────────────────────────────────

    [Scenario("NormalizeAsync UnregisteredKeyWithoutDefault ThrowsKeyNotFoundException")]
    [Fact]
    public async Task NormalizeAsync_UnregisteredKeyWithoutDefault_ThrowsKeyNotFoundException()
    {
        var normalizer = KeyedNormalizer<string, string, Order>.Create()
            .When("json", (raw, _) => new ValueTask<Order>(new Order(raw, "json")))
            .Build();

        await ScenarioExpect.ThrowsAsync<KeyNotFoundException>(
            () => normalizer.NormalizeAsync("xml", "data").AsTask());
    }

    // ─── Async handler completes asynchronously ───────────────────────────────

    [Scenario("NormalizeAsync AsyncHandlerCompletesAsynchronously")]
    [Fact]
    public async Task NormalizeAsync_AsyncHandlerCompletesAsynchronously()
    {
        var normalizer = KeyedNormalizer<string, string, Order>.Create()
            .When("slow", async (raw, ct) =>
            {
                await Task.Yield();
                return new Order(raw, "slow");
            })
            .Build();

        var result = await normalizer.NormalizeAsync("slow", "data");

        ScenarioExpect.Equal("slow", result.Format);
    }

    // ─── Cancellation propagates ──────────────────────────────────────────────

    [Scenario("NormalizeAsync CancellationPropagates")]
    [Fact]
    public async Task NormalizeAsync_CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var normalizer = KeyedNormalizer<string, string, Order>.Create()
            .When("json", async (raw, ct) =>
            {
                await Task.Delay(1000, ct);
                return new Order(raw, "json");
            })
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(
            () => normalizer.NormalizeAsync("json", "data", cts.Token).AsTask());
    }

    // ─── Duplicate key registration ───────────────────────────────────────────

    [Scenario("Builder DuplicateKey ThrowsArgumentException")]
    [Fact]
    public void Builder_DuplicateKey_ThrowsArgumentException()
    {
        ScenarioExpect.Throws<ArgumentException>(() =>
            KeyedNormalizer<string, string, Order>.Create()
                .When("json", (raw, _) => new ValueTask<Order>(new Order(raw, "json")))
                .When("json", (raw, _) => new ValueTask<Order>(new Order(raw, "json2")))
                .Build());
    }

    // ─── Build semantics ──────────────────────────────────────────────────────

    [Scenario("Builder BuildCalledTwice ReturnsDistinctInstances")]
    [Fact]
    public void Builder_BuildCalledTwice_ReturnsDistinctInstances()
    {
        var builder = KeyedNormalizer<string, string, Order>.Create()
            .When("json", (raw, _) => new ValueTask<Order>(new Order(raw, "json")));

        var a = builder.Build();
        var b = builder.Build();

        // Each call produces an independent snapshot — not the same reference.
        ScenarioExpect.False(ReferenceEquals(a, b));
    }

    // ─── Builder validation ───────────────────────────────────────────────────

    [Scenario("Builder NoHandlers ThrowsInvalidOperationException")]
    [Fact]
    public void Builder_NoHandlers_ThrowsInvalidOperationException()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            KeyedNormalizer<string, string, Order>.Create().Build());
    }

    private sealed record Order(string Id, string Format);
}
