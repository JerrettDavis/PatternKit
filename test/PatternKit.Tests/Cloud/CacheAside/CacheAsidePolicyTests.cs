using PatternKit.Cloud.CacheAside;
using TinyBDD;

namespace PatternKit.Tests.Cloud.CacheAside;

public sealed class CacheAsidePolicyTests
{
    [Scenario("Cache-aside policy loads on miss and reads from cache on hit")]
    [Fact]
    public void CacheAside_Loads_On_Miss_And_Reads_From_Cache_On_Hit()
    {
        var calls = 0;
        var policy = CacheAsidePolicy<string>.Create("products").Build();

        var first = policy.GetOrLoad("SKU-42", () =>
        {
            calls++;
            return "Widget";
        });
        var second = policy.GetOrLoad("SKU-42", () =>
        {
            calls++;
            return "Other";
        });

        ScenarioExpect.True(first.Found);
        ScenarioExpect.True(first.CacheMiss);
        ScenarioExpect.Equal("Widget", first.Value);
        ScenarioExpect.True(second.CacheHit);
        ScenarioExpect.Equal("Widget", second.Value);
        ScenarioExpect.Equal(1, calls);
    }

    [Scenario("Cache-aside policy skips missing origin values")]
    [Fact]
    public void CacheAside_Skips_Missing_Origin_Values()
    {
        var calls = 0;
        var policy = CacheAsidePolicy<string>.Create("products").Build();

        var first = policy.GetOrLoad("missing", () =>
        {
            calls++;
            return null;
        });
        var second = policy.GetOrLoad("missing", () =>
        {
            calls++;
            return null;
        });

        ScenarioExpect.False(first.Found);
        ScenarioExpect.False(first.CacheHit);
        ScenarioExpect.False(second.Found);
        ScenarioExpect.Equal(2, calls);
    }

    [Scenario("Cache-aside policy expires entries by TTL")]
    [Fact]
    public void CacheAside_Expires_Entries_By_Ttl()
    {
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        var store = new InMemoryCacheAsideStore<string>(() => now);
        var calls = 0;
        var policy = CacheAsidePolicy<string>.Create("products")
            .WithStore(store)
            .WithTimeToLive(TimeSpan.FromMilliseconds(10))
            .Build();

        _ = policy.GetOrLoad("SKU-42", () =>
        {
            calls++;
            return "first";
        });
        now = now.AddMilliseconds(10);
        var expired = policy.GetOrLoad("SKU-42", () =>
        {
            calls++;
            return "second";
        });

        ScenarioExpect.True(expired.CacheMiss);
        ScenarioExpect.Equal("second", expired.Value);
        ScenarioExpect.Equal(2, calls);
    }

    [Scenario("Cache-aside policy honors cache predicates and invalidation")]
    [Fact]
    public void CacheAside_Honors_Cache_Predicates_And_Invalidation()
    {
        var calls = 0;
        var policy = CacheAsidePolicy<int>.Create("prices")
            .CacheWhen(static value => value > 0)
            .Build();

        _ = policy.GetOrLoad("free", () => 0);
        _ = policy.GetOrLoad("free", () => 0);
        _ = policy.GetOrLoad("paid", () =>
        {
            calls++;
            return 10;
        });
        var hit = policy.GetOrLoad("paid", () => 20);
        var removed = policy.Invalidate("paid");
        var reloaded = policy.GetOrLoad("paid", () => 30);

        ScenarioExpect.Equal(1, calls);
        ScenarioExpect.True(hit.CacheHit);
        ScenarioExpect.Equal(10, hit.Value);
        ScenarioExpect.True(removed);
        ScenarioExpect.True(reloaded.CacheMiss);
        ScenarioExpect.Equal(30, reloaded.Value);
    }

    [Scenario("Async cache-aside policy preserves cancellation")]
    [Fact]
    public async Task AsyncCacheAside_Preserves_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var policy = CacheAsidePolicy<string>.Create("cancel").Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() =>
            policy.GetOrLoadAsync("SKU-42", static _ => new ValueTask<string?>("never"), cts.Token).AsTask());
    }

    [Scenario("Cache-aside policy rejects invalid configuration")]
    [Fact]
    public async Task CacheAside_Rejects_Invalid_Configuration()
    {
        var policy = CacheAsidePolicy<string>.Create("nulls").Build();

        ScenarioExpect.Throws<ArgumentException>(() => CacheAsidePolicy<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => CacheAsidePolicy<string>.Create().WithTimeToLive(TimeSpan.FromMilliseconds(-1)).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => CacheAsidePolicy<string>.Create().WithStore(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => CacheAsidePolicy<string>.Create().CacheWhen(null!));
        ScenarioExpect.Throws<ArgumentException>(() => policy.GetOrLoad("", static () => "value"));
        ScenarioExpect.Throws<ArgumentNullException>(() => policy.GetOrLoad("key", null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => policy.GetOrLoadAsync("key", null!).AsTask());
    }
}
