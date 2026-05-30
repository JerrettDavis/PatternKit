using PatternKit.Cloud.ReadWriteThroughCache;
using TinyBDD;

namespace PatternKit.Tests.Cloud.ReadWriteThroughCache;

public sealed class ReadWriteThroughCachePolicyTests
{
    [Scenario("Read-through cache loads misses and reuses cached values")]
    [Fact]
    public async Task ReadThrough_Cache_Loads_Misses_And_Reuses_Cached_Values()
    {
        var policy = ReadWriteThroughCachePolicy<string>.Create("products").WithTimeToLive(TimeSpan.FromMinutes(1)).Build();
        var loads = 0;

        ValueTask<string?> Loader(CancellationToken _)
            => new($"catalog-{Interlocked.Increment(ref loads)}");

        var miss = await policy.ReadThroughAsync("sku-1", Loader);
        var hit = await policy.ReadThroughAsync("sku-1", Loader);

        ScenarioExpect.Equal("products", policy.Name);
        ScenarioExpect.Equal(TimeSpan.FromMinutes(1), policy.TimeToLive);
        ScenarioExpect.False(miss.CacheHit);
        ScenarioExpect.True(miss.Found);
        ScenarioExpect.True(hit.CacheHit);
        ScenarioExpect.Equal("catalog-1", hit.Value);
        ScenarioExpect.Equal(1, loads);
    }

    [Scenario("Write-through cache persists before updating cache")]
    [Fact]
    public async Task WriteThrough_Cache_Persists_Before_Updating_Cache()
    {
        var policy = ReadWriteThroughCachePolicy<string>.Create().Build();
        var persisted = new List<string>();

        var written = await policy.WriteThroughAsync("sku-1", "new-value", (value, _) =>
        {
            persisted.Add(value);
            return ValueTask.CompletedTask;
        });
        var read = await policy.ReadThroughAsync("sku-1", static _ => new ValueTask<string?>("origin"));

        ScenarioExpect.True(written.Written);
        ScenarioExpect.Equal("new-value", written.Value);
        ScenarioExpect.Equal(["new-value"], persisted);
        ScenarioExpect.True(read.CacheHit);
        ScenarioExpect.Equal("new-value", read.Value);
    }

    [Scenario("Read/write-through cache releases misses and validates input")]
    [Fact]
    public async Task ReadWriteThrough_Cache_Releases_Misses_And_Validates_Input()
    {
        var policy = ReadWriteThroughCachePolicy<string>.Create().WithoutExpiration().Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var missing = await policy.ReadThroughAsync("missing", static _ => new ValueTask<string?>((string?)null));

        ScenarioExpect.False(missing.Found);
        ScenarioExpect.False(missing.CacheHit);
        ScenarioExpect.Throws<ArgumentException>(() => ReadWriteThroughCachePolicy<string>.Create(" ").Build());
        await ScenarioExpect.ThrowsAsync<ArgumentException>(async () => await policy.ReadThroughAsync("", static _ => new ValueTask<string?>("value")));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await policy.ReadThroughAsync("sku-1", null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await policy.WriteThroughAsync("sku-1", "value", null!));
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () => await policy.ReadThroughAsync("sku-1", static _ => new ValueTask<string?>("value"), cts.Token));
    }

    [Scenario("Read write through cache exposes store operations and builder validation")]
    [Fact]
    public async Task ReadWriteThrough_Cache_Exposes_Store_Operations_And_Builder_Validation()
    {
        var store = new PatternKit.Cloud.CacheAside.InMemoryCacheAsideStore<string>();
        var builder = ReadWriteThroughCachePolicy<string>.Create("custom");
        var returned = builder.WithStore(store);
        var policy = builder.Build();

        var written = await policy.WriteThroughAsync("sku-1", "value", static (_, _) => ValueTask.CompletedTask);
        var invalidated = policy.Invalidate("sku-1");
        await policy.WriteThroughAsync("sku-2", "other", static (_, _) => ValueTask.CompletedTask);
        policy.Clear();
        var afterClear = await policy.ReadThroughAsync("sku-2", static _ => new ValueTask<string?>((string?)null));

        ScenarioExpect.Same(builder, returned);
        ScenarioExpect.Same(store, policy.Store);
        ScenarioExpect.Equal("sku-1", written.Key);
        ScenarioExpect.True(written.CacheMiss);
        ScenarioExpect.True(invalidated);
        ScenarioExpect.True(afterClear.CacheMiss);
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => ReadWriteThroughCachePolicy<string>.Create().WithTimeToLive(TimeSpan.FromMilliseconds(-1)).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => ReadWriteThroughCachePolicy<string>.Create().WithStore(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentException>(async () => await policy.WriteThroughAsync("", "value", static (_, _) => ValueTask.CompletedTask));
    }
}
