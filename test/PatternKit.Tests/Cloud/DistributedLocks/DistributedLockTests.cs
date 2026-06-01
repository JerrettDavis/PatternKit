using PatternKit.Cloud.DistributedLocks;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.DistributedLocks;

[Feature("Distributed Lock")]
public sealed class DistributedLockTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Distributed lock acquires renews and releases leases")]
    [Fact]
    public Task Distributed_Lock_Acquires_Renews_And_Releases_Leases()
        => Given("a distributed lock with a fixed clock", () =>
        {
            var now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");
            var mutex = DistributedLock<string>.Create("orders-lock")
                .LeaseDuration(TimeSpan.FromSeconds(5))
                .WithClock(() => now)
                .Build();
            return new { Lock = mutex, Advance = new Action<TimeSpan>(delta => now = now.Add(delta)) };
        })
        .When("an owner acquires renews and releases a resource lease", ctx =>
        {
            var acquired = ctx.Lock.TryAcquire("order-1", "worker-a");
            ctx.Advance(TimeSpan.FromSeconds(2));
            var renewed = ctx.Lock.Renew(acquired.Lease!);
            var released = ctx.Lock.Release(renewed.Lease!);
            return new { acquired, renewed, released, ctx.Lock };
        })
        .Then("each lock transition succeeds and clears the gate", result =>
        {
            ScenarioExpect.True(result.acquired.Acquired);
            ScenarioExpect.Equal("orders-lock", result.acquired.LockName);
            ScenarioExpect.Equal("order-1", result.acquired.ResourceKey);
            ScenarioExpect.Equal("worker-a", result.acquired.OwnerId);
            ScenarioExpect.True(result.renewed.Renewed);
            ScenarioExpect.True(result.renewed.Lease!.ExpiresAt > result.acquired.Lease!.ExpiresAt);
            ScenarioExpect.True(result.released.Released);
            ScenarioExpect.Equal(result.renewed.Lease.ExpiresAt, result.released.Lease!.ExpiresAt);
            ScenarioExpect.False(result.Lock.IsBlocked);
            ScenarioExpect.Equal(0, result.Lock.ActiveCount);
            var state = result.Lock.GetState();
            ScenarioExpect.Equal("orders-lock", state.LockName);
            ScenarioExpect.False(state.IsBlocked);
            ScenarioExpect.Equal(0, state.ActiveCount);
            ScenarioExpect.Empty(state.ActiveLeases);
        })
        .AssertPassed();

    [Scenario("Distributed lock captures a single clock value for lease timestamps")]
    [Fact]
    public Task Distributed_Lock_Captures_A_Single_Clock_Value_For_Lease_Timestamps()
        => Given("a distributed lock with an advancing clock", () =>
        {
            var now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");
            return DistributedLock<string>.Create("orders-lock")
                .LeaseDuration(TimeSpan.FromSeconds(5))
                .WithClock(() =>
                {
                    var value = now;
                    now = now.AddSeconds(1);
                    return value;
                })
                .Build();
        })
        .When("a lease is acquired and renewed", mutex =>
        {
            var acquired = mutex.TryAcquire("order-1", "worker-a");
            var renewed = mutex.Renew(acquired.Lease!);
            return new { acquired, renewed };
        })
        .Then("the expiration timestamps are derived from the same captured instant", result =>
        {
            ScenarioExpect.Equal(result.acquired.Lease!.AcquiredAt.AddSeconds(5), result.acquired.Lease.ExpiresAt);
            ScenarioExpect.Equal(result.acquired.Lease.AcquiredAt.AddSeconds(6), result.renewed.Lease!.ExpiresAt);
        })
        .AssertPassed();

    [Scenario("Distributed lock blocks contention until lease expiry")]
    [Fact]
    public Task Distributed_Lock_Blocks_Contention_Until_Lease_Expiry()
        => Given("a distributed lock with two owners", () =>
        {
            var now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");
            var mutex = DistributedLock<string>.Create("orders-lock")
                .LeaseDuration(TimeSpan.FromSeconds(1))
                .WithClock(() => now)
                .Build();
            return new { Lock = mutex, Advance = new Action<TimeSpan>(delta => now = now.Add(delta)) };
        })
        .When("a second owner contends before and after expiry", ctx =>
        {
            var first = ctx.Lock.TryAcquire("order-1", "worker-a");
            var blocked = ctx.Lock.TryAcquire("order-1", "worker-b");
            ctx.Advance(TimeSpan.FromSeconds(2));
            var afterExpiry = ctx.Lock.TryAcquire("order-1", "worker-b");
            return new { first, blocked, afterExpiry, ctx.Lock };
        })
        .Then("contention fails while the lease is active and succeeds after expiry", result =>
        {
            ScenarioExpect.True(result.first.Acquired);
            ScenarioExpect.True(result.blocked.Failed);
            ScenarioExpect.Contains("worker-a", result.blocked.Exception!.Message);
            ScenarioExpect.True(result.afterExpiry.Acquired);
            ScenarioExpect.Equal("worker-b", result.Lock.Snapshot().Single().OwnerId);
        })
        .AssertPassed();

    [Scenario("Distributed lock validates inputs and stale leases")]
    [Fact]
    public Task Distributed_Lock_Validates_Inputs_And_Stale_Leases()
        => Given("invalid distributed lock inputs", () => true)
        .Then("invalid configuration and owner values are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => DistributedLock<string>.Create("").Build());
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => DistributedLock<string>.Create().LeaseDuration(TimeSpan.Zero).Build());
            ScenarioExpect.Throws<ArgumentNullException>(() => DistributedLock<string>.Create().WithClock(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => DistributedLock<string>.Create().WithKeyComparer(null!));
            ScenarioExpect.Throws<ArgumentException>(() => DistributedLock<string>.Create().Build().TryAcquire("order-1", ""));
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => DistributedLock<string>.Create().Build().TryAcquire("order-1", "worker-a", TimeSpan.Zero));
        })
        .And("stale or mismatched leases cannot renew or release active locks", _ =>
        {
            var mutex = DistributedLock<string>.Create().Build();
            ScenarioExpect.Throws<ArgumentNullException>(() => mutex.Renew(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => mutex.Release(null!));
            var acquired = mutex.TryAcquire("order-1", "worker-a");
            var mismatched = new DistributedLockRecord<string>("order-1", "worker-b", acquired.Lease!.Token, acquired.Lease.AcquiredAt, acquired.Lease.ExpiresAt);
            ScenarioExpect.True(mutex.Renew(mismatched).Failed);
            ScenarioExpect.True(mutex.Release(mismatched).Failed);
            ScenarioExpect.True(mutex.Release(acquired.Lease).Released);
            ScenarioExpect.True(mutex.Renew(acquired.Lease).Failed);
            ScenarioExpect.True(mutex.Release(acquired.Lease).Failed);
        })
        .And("records states and results guard required values", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => new DistributedLockRecord<string>("order-1", "", "token", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            ScenarioExpect.Throws<ArgumentException>(() => new DistributedLockRecord<string>("order-1", "owner", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            ScenarioExpect.Throws<ArgumentNullException>(() => DistributedLockResult<string>.Failure("lock", "order-1", "owner", null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => new DistributedLockState<string>("lock", false, 0, null!));
        })
        .AssertPassed();

    [Scenario("Distributed lock honors custom key comparers")]
    [Fact]
    public Task Distributed_Lock_Honors_Custom_Key_Comparers()
        => Given("a distributed lock with a case-insensitive key comparer", () =>
            DistributedLock<string>.Create("orders-lock")
                .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
                .Build())
        .When("a lease is acquired with one key casing and released with the returned lease", mutex =>
        {
            var acquired = mutex.TryAcquire("ORDER-1", "worker-a");
            var blocked = mutex.TryAcquire("order-1", "worker-b");
            var released = mutex.Release(acquired.Lease!);
            return new { acquired, blocked, released, mutex };
        })
        .Then("the comparer controls contention and release succeeds", result =>
        {
            ScenarioExpect.True(result.acquired.Acquired);
            ScenarioExpect.True(result.blocked.Failed);
            ScenarioExpect.True(result.released.Released);
            ScenarioExpect.False(result.mutex.IsLocked("order-1"));
        })
        .AssertPassed();
}
