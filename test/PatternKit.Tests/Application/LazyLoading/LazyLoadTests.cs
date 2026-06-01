using PatternKit.Application.LazyLoading;
using TinyBDD;

namespace PatternKit.Tests.Application.LazyLoading;

public sealed class LazyLoadTests
{
    [Scenario("Lazy load defers and caches expensive values")]
    [Fact]
    public async Task Lazy_Load_Defers_And_Caches_Expensive_Values()
    {
        var calls = 0;
        var loader = LazyLoad<string>.Create("profile")
            .LoadWith(_ =>
            {
                calls++;
                return new ValueTask<string>("customer");
            })
            .Build();

        ScenarioExpect.False(loader.IsLoaded);
        var first = await loader.GetAsync();
        var second = await loader.GetAsync();

        ScenarioExpect.True(first.Loaded);
        ScenarioExpect.False(second.Loaded);
        ScenarioExpect.True(second.Cached);
        ScenarioExpect.Equal("customer", second.Value);
        ScenarioExpect.Equal(1, calls);
        ScenarioExpect.True(loader.IsLoaded);
    }

    [Scenario("Lazy load invalidates cached values")]
    [Fact]
    public async Task Lazy_Load_Invalidates_Cached_Values()
    {
        var calls = 0;
        var loader = LazyLoad<int>.Create("counter")
            .LoadWith(_ => new ValueTask<int>(++calls))
            .Build();

        var first = await loader.GetAsync();
        loader.Invalidate();
        var second = await loader.GetAsync();

        ScenarioExpect.Equal(1, first.Value);
        ScenarioExpect.Equal(2, second.Value);
        ScenarioExpect.True(second.Loaded);
    }

    [Scenario("Lazy load invalidation wins over in-flight load")]
    [Fact]
    public async Task Lazy_Load_Invalidation_Wins_Over_In_Flight_Load()
    {
        var firstLoad = new TaskCompletionSource<int>();
        var calls = 0;
        var loader = LazyLoad<int>.Create("in-flight")
            .LoadWith(_ =>
            {
                calls++;
                return calls == 1
                    ? new ValueTask<int>(firstLoad.Task)
                    : new ValueTask<int>(calls);
            })
            .Build();

        var pending = loader.GetAsync();
        loader.Invalidate();
        firstLoad.SetResult(1);

        var first = await pending;
        var second = await loader.GetAsync();

        ScenarioExpect.Equal(1, first.Value);
        ScenarioExpect.True(first.Loaded);
        ScenarioExpect.Equal(2, second.Value);
        ScenarioExpect.True(second.Loaded);
    }

    [Scenario("Lazy load can disable caching")]
    [Fact]
    public async Task Lazy_Load_Can_Disable_Caching()
    {
        var calls = 0;
        var loader = LazyLoad<int>.Create("uncached")
            .LoadWith(_ => new ValueTask<int>(++calls))
            .DisableCache()
            .Build();

        var first = await loader.GetAsync();
        var second = await loader.GetAsync();

        ScenarioExpect.Equal(1, first.Value);
        ScenarioExpect.Equal(2, second.Value);
        ScenarioExpect.False(loader.IsLoaded);
    }

    [Scenario("Lazy load reloads expired values")]
    [Fact]
    public async Task Lazy_Load_Reloads_Expired_Values()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var calls = 0;
        var loader = LazyLoad<int>.Create("ttl")
            .LoadWith(_ => new ValueTask<int>(++calls))
            .WithTimeToLive(TimeSpan.FromSeconds(1))
            .WithClock(time.GetUtcNow)
            .Build();

        var first = await loader.GetAsync();
        time.Advance(TimeSpan.FromSeconds(2));
        var second = await loader.GetAsync();

        ScenarioExpect.Equal(1, first.Value);
        ScenarioExpect.Equal(2, second.Value);
        ScenarioExpect.True(second.Loaded);
    }

    [Scenario("Lazy load preserves cancellation and validation")]
    [Fact]
    public async Task Lazy_Load_Preserves_Cancellation_And_Validation()
    {
        ScenarioExpect.Throws<ArgumentException>(() => LazyLoad<string>.Create("").LoadWith(_ => new("")).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => LazyLoad<string>.Create().LoadWith(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => LazyLoad<string>.Create().LoadWith(_ => new("")).WithClock(null!));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => LazyLoad<string>.Create().LoadWith(_ => new("")).WithTimeToLive(TimeSpan.Zero));
        ScenarioExpect.Throws<InvalidOperationException>(() => LazyLoad<string>.Create().Build());

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var loader = LazyLoad<string>.Create()
            .LoadWith(_ => new ValueTask<string>("never"))
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => loader.GetAsync(cts.Token).AsTask());
    }

    private sealed class FakeTimeProvider(DateTimeOffset now)
    {
        private DateTimeOffset _now = now;

        public DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
