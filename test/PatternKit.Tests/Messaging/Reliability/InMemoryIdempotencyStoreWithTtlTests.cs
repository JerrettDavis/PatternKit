using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class InMemoryIdempotencyStoreWithTtlTests
{
    [Scenario("TryClaimAsync ClaimsNewKey")]
    [Fact]
    public async Task TryClaimAsync_ClaimsNewKey()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();

        var claim = await store.TryClaimAsync("key-1");

        ScenarioExpect.True(claim.Claimed);
        ScenarioExpect.Equal("key-1", claim.Key);
    }

    [Scenario("TryClaimAsync DoesNotClaimExistingKey")]
    [Fact]
    public async Task TryClaimAsync_DoesNotClaimExistingKey()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();
        await store.TryClaimAsync("key-1");

        var second = await store.TryClaimAsync("key-1");

        ScenarioExpect.False(second.Claimed);
    }

    [Scenario("TryClaimAsync WithTtl ClaimsExpiredKey")]
    [Fact]
    public async Task TryClaimAsync_WithTtl_ClaimsExpiredKey()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();
        await store.TryClaimAsync("key-1", TimeSpan.FromMilliseconds(20));

        await Task.Delay(50); // wait for TTL to expire

        var second = await store.TryClaimAsync("key-1");
        ScenarioExpect.True(second.Claimed);
    }

    [Scenario("TryClaimAsync WithTtl DoesNotClaimActiveKey")]
    [Fact]
    public async Task TryClaimAsync_WithTtl_DoesNotClaimActiveKey()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();
        await store.TryClaimAsync("key-1", TimeSpan.FromMinutes(10));

        var second = await store.TryClaimAsync("key-1");
        ScenarioExpect.False(second.Claimed);
    }

    [Scenario("EvictExpiredAsync RemovesExpiredKeys")]
    [Fact]
    public async Task EvictExpiredAsync_RemovesExpiredKeys()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();
        await store.TryClaimAsync("key-expire", TimeSpan.FromMilliseconds(20));
        await store.TryClaimAsync("key-keep", TimeSpan.FromMinutes(10));

        await Task.Delay(50);
        var evicted = await store.EvictExpiredAsync();

        ScenarioExpect.Equal(1, evicted);
        ScenarioExpect.Equal(1, store.Count);
    }

    [Scenario("EvictExpiredAsync NoExpiredKeys ReturnsZero")]
    [Fact]
    public async Task EvictExpiredAsync_NoExpiredKeys_ReturnsZero()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();
        await store.TryClaimAsync("key-1", TimeSpan.FromMinutes(5));

        var evicted = await store.EvictExpiredAsync();

        ScenarioExpect.Equal(0, evicted);
    }

    [Scenario("MarkCompletedAsync KeepsExistingTtl")]
    [Fact]
    public async Task MarkCompletedAsync_KeepsExistingTtl()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();
        await store.TryClaimAsync("key-1", TimeSpan.FromMilliseconds(20));
        await store.MarkCompletedAsync("key-1", "result");

        await Task.Delay(50);
        // After TTL, should be claimable again
        var third = await store.TryClaimAsync("key-1");
        ScenarioExpect.True(third.Claimed);
    }

    [Scenario("TryClaimAsync RejectsEmptyKey")]
    [Fact]
    public async Task TryClaimAsync_RejectsEmptyKey()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();

        await ScenarioExpect.ThrowsAsync<ArgumentException>(() => store.TryClaimAsync("").AsTask());
    }

    [Scenario("ConcurrentClaims OnlyOneSucceeds")]
    [Fact]
    public async Task ConcurrentClaims_OnlyOneSucceeds()
    {
        var store = new InMemoryIdempotencyStoreWithTtl();
        var claimCount = 0;

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                var claim = await store.TryClaimAsync("shared-key");
                if (claim.Claimed)
                    Interlocked.Increment(ref claimCount);
            }));

        await Task.WhenAll(tasks);

        ScenarioExpect.Equal(1, claimCount);
    }
}
