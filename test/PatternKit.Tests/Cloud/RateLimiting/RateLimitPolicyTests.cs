using PatternKit.Cloud.RateLimiting;
using TinyBDD;

namespace PatternKit.Tests.Cloud.RateLimiting;

public sealed class RateLimitPolicyTests
{
    [Scenario("Rate-limit policy allows permits inside a fixed window and rejects overflow")]
    [Fact]
    public void RateLimit_Allows_Permits_Inside_Window_And_Rejects_Overflow()
    {
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        var calls = 0;
        var policy = RateLimitPolicy<string>.Create("tenant-search")
            .WithPermitLimit(2)
            .WithWindow(TimeSpan.FromSeconds(30))
            .WithClock(() => now)
            .Build();

        var first = policy.Execute("tenant-a", () => (++calls).ToString());
        var second = policy.Execute("tenant-a", () => (++calls).ToString());
        var third = policy.Execute("tenant-a", () => (++calls).ToString());

        ScenarioExpect.True(first.Allowed);
        ScenarioExpect.Equal(1, first.RemainingPermits);
        ScenarioExpect.True(second.Allowed);
        ScenarioExpect.Equal(0, second.RemainingPermits);
        ScenarioExpect.True(third.Rejected);
        ScenarioExpect.Equal(now.AddSeconds(30), third.RetryAfter);
        ScenarioExpect.Equal(2, calls);
    }

    [Scenario("Rate-limit policy resets permits when the window advances")]
    [Fact]
    public void RateLimit_Resets_Permits_When_Window_Advances()
    {
        var now = DateTimeOffset.Parse("2026-05-20T00:00:00Z");
        var policy = RateLimitPolicy<string>.Create("tenant-search")
            .WithPermitLimit(1)
            .WithWindow(TimeSpan.FromSeconds(1))
            .WithClock(() => now)
            .Build();

        var allowed = policy.Execute("tenant-a", static () => "first");
        var rejected = policy.Execute("tenant-a", static () => "blocked");
        now = now.AddSeconds(1);
        var reopened = policy.Execute("tenant-a", static () => "second");

        ScenarioExpect.True(allowed.Allowed);
        ScenarioExpect.True(rejected.Rejected);
        ScenarioExpect.True(reopened.Allowed);
        ScenarioExpect.Equal("second", reopened.Value);
    }

    [Scenario("Rate-limit policy partitions permits by key")]
    [Fact]
    public void RateLimit_Partitions_Permits_By_Key()
    {
        var policy = RateLimitPolicy<string>.Create("tenant-search")
            .WithPermitLimit(1)
            .Build();

        var tenantA = policy.Execute("tenant-a", static () => "a");
        var tenantB = policy.Execute("tenant-b", static () => "b");
        var tenantAOverflow = policy.Execute("tenant-a", static () => "overflow");

        ScenarioExpect.True(tenantA.Allowed);
        ScenarioExpect.True(tenantB.Allowed);
        ScenarioExpect.True(tenantAOverflow.Rejected);
    }

    [Scenario("Rate-limit policy supports explicit partition reset")]
    [Fact]
    public void RateLimit_Supports_Explicit_Partition_Reset()
    {
        var policy = RateLimitPolicy<string>.Create("tenant-search")
            .WithPermitLimit(1)
            .Build();

        _ = policy.Execute("tenant-a", static () => "first");
        var reset = policy.Reset("tenant-a");
        var reopened = policy.Execute("tenant-a", static () => "second");

        ScenarioExpect.True(reset);
        ScenarioExpect.True(reopened.Allowed);
        ScenarioExpect.Equal("second", reopened.Value);
    }

    [Scenario("Async rate-limit policy preserves cancellation")]
    [Fact]
    public async Task AsyncRateLimit_Preserves_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var policy = RateLimitPolicy<string>.Create("cancel").Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync("tenant-a", static _ => new ValueTask<string>("never"), cts.Token).AsTask());
    }

    [Scenario("Rate-limit policy consumes permits before operation failure")]
    [Fact]
    public void RateLimit_Consumes_Permits_Before_Operation_Failure()
    {
        var policy = RateLimitPolicy<string>.Create("tenant-search")
            .WithPermitLimit(1)
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => policy.Execute("tenant-a", static () => throw new InvalidOperationException("origin failed")));
        var rejected = policy.Execute("tenant-a", static () => "second");

        ScenarioExpect.True(rejected.Rejected);
    }

    [Scenario("Rate-limit policy rejects invalid configuration")]
    [Fact]
    public async Task RateLimit_Rejects_Invalid_Configuration()
    {
        var policy = RateLimitPolicy<string>.Create("valid").Build();

        ScenarioExpect.Throws<ArgumentException>(() => RateLimitPolicy<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => RateLimitPolicy<string>.Create().WithPermitLimit(0).Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => RateLimitPolicy<string>.Create().WithWindow(TimeSpan.Zero).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => RateLimitPolicy<string>.Create().WithClock(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => RateLimitPolicy<string>.Create().WithKeyComparer(null!));
        ScenarioExpect.Throws<ArgumentException>(() => policy.Execute("", static () => "value"));
        ScenarioExpect.Throws<ArgumentNullException>(() => policy.Execute("tenant-a", null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => policy.ExecuteAsync("tenant-a", null!).AsTask());
    }
}
