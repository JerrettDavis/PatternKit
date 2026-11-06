using PatternKit.Cloud.CacheStampedeProtection;
using TinyBDD;

namespace PatternKit.Tests.Cloud.CacheStampedeProtection;

public sealed class CacheStampedeProtectionPolicyTests
{
    [Scenario("Cache stampede protection shares concurrent loads by key")]
    [Fact]
    public async Task Cache_Stampede_Protection_Shares_Concurrent_Loads_By_Key()
    {
        var policy = CacheStampedeProtectionPolicy<string>.Create("products").Build();
        var release = new TaskCompletionSource<bool>();
        var loadCount = 0;

        async ValueTask<string> Loader(CancellationToken _)
        {
            Interlocked.Increment(ref loadCount);
            await release.Task;
            return "catalog";
        }

        var first = policy.GetOrLoadAsync("sku-1", Loader).AsTask();
        var second = policy.GetOrLoadAsync("sku-1", Loader).AsTask();

        ScenarioExpect.Equal(1, policy.InFlightCount);
        release.SetResult(true);

        var results = await Task.WhenAll(first, second);

        ScenarioExpect.Equal(1, loadCount);
        ScenarioExpect.Equal("catalog", results[0].Value);
        ScenarioExpect.False(results[0].SharedFlight);
        ScenarioExpect.True(results[1].SharedFlight);
        ScenarioExpect.Equal(0, policy.InFlightCount);
    }

    [Scenario("Cache stampede protection isolates different keys")]
    [Fact]
    public async Task Cache_Stampede_Protection_Isolates_Different_Keys()
    {
        var policy = CacheStampedeProtectionPolicy<string>.Create().Build();
        var loadCount = 0;

        ValueTask<string> Loader(CancellationToken _)
            => new($"value-{Interlocked.Increment(ref loadCount)}");

        var first = await policy.GetOrLoadAsync("sku-1", Loader);
        var second = await policy.GetOrLoadAsync("sku-2", Loader);

        ScenarioExpect.Equal("cache-stampede-protection", policy.Name);
        ScenarioExpect.Equal("sku-1", first.Key);
        ScenarioExpect.Equal("sku-2", second.Key);
        ScenarioExpect.Equal(2, loadCount);
        ScenarioExpect.False(first.SharedFlight);
        ScenarioExpect.False(second.SharedFlight);
    }

    [Scenario("Cache stampede protection releases failed loads")]
    [Fact]
    public async Task Cache_Stampede_Protection_Releases_Failed_Loads()
    {
        var policy = CacheStampedeProtectionPolicy<string>.Create().Build();
        var attempts = 0;

        async ValueTask<string> Loader(CancellationToken _)
        {
            await Task.Yield();
            if (Interlocked.Increment(ref attempts) == 1)
                throw new InvalidOperationException("origin unavailable");
            return "recovered";
        }

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(async () => await policy.GetOrLoadAsync("sku-1", Loader));
        var recovered = await policy.GetOrLoadAsync("sku-1", Loader);

        ScenarioExpect.Equal("recovered", recovered.Value);
        ScenarioExpect.Equal(2, attempts);
        ScenarioExpect.Equal(0, policy.InFlightCount);
    }

    [Scenario("Cache stampede protection honors cancellation and validates configuration")]
    [Fact]
    public async Task Cache_Stampede_Protection_Honors_Cancellation_And_Validates_Configuration()
    {
        var policy = CacheStampedeProtectionPolicy<string>.Create().Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        ScenarioExpect.Throws<ArgumentException>(() => CacheStampedeProtectionPolicy<string>.Create("").Build());
        await ScenarioExpect.ThrowsAsync<ArgumentException>(async () => await policy.GetOrLoadAsync("", static _ => new ValueTask<string>("value")));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await policy.GetOrLoadAsync("sku-1", null!));
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () => await policy.GetOrLoadAsync("sku-1", static _ => new ValueTask<string>("value"), cts.Token));
    }

    [Scenario("Cache stampede protection coordinates cancellable waits")]
    [Fact]
    public async Task Cache_Stampede_Protection_Coordinates_Cancellable_Waits()
    {
        var policy = CacheStampedeProtectionPolicy<string>.Create().Build();
        using var activeTokenSource = new CancellationTokenSource();
        var release = new TaskCompletionSource<bool>();

        async ValueTask<string> Loader(CancellationToken _)
        {
            await release.Task;
            return "catalog";
        }

        var pending = policy.GetOrLoadAsync("sku-1", Loader, activeTokenSource.Token).AsTask();
        release.SetResult(true);
        var completed = await pending;
        var fastPath = await policy.GetOrLoadAsync("sku-2", static _ => new ValueTask<string>("ready"), activeTokenSource.Token);

        using var cancelledWait = new CancellationTokenSource();
        var blocked = new TaskCompletionSource<bool>();
        var cancelled = policy.GetOrLoadAsync("sku-3", async _ =>
        {
            await blocked.Task;
            return "late";
        }, cancelledWait.Token).AsTask();
        var follower = policy.GetOrLoadAsync("sku-3", static _ => new ValueTask<string>("unused")).AsTask();

        cancelledWait.Cancel();
        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () => await cancelled);
        blocked.SetResult(true);
        var followed = await follower;

        ScenarioExpect.Equal("catalog", completed.Value);
        ScenarioExpect.Equal("ready", fastPath.Value);
        ScenarioExpect.Equal("late", followed.Value);
        ScenarioExpect.Equal(0, policy.InFlightCount);
    }
}
