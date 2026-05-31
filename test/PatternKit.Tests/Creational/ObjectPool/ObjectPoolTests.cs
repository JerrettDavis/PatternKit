using PatternKit.Creational.ObjectPool;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Creational.ObjectPool;

[Feature("Creational - Object Pool")]
public sealed class ObjectPoolTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Returned items are reset and reused")]
    [Fact]
    public Task Returned_Items_Are_Reset_And_Reused()
        => Given("an object pool with return reset behavior", () => ObjectPool<PooledBuffer>.Create()
            .WithFactory(static () => new PooledBuffer())
            .OnReturn(static buffer => buffer.Reset())
            .WithMaxRetained(1)
            .Build())
        .When("renting returning and renting again", pool =>
        {
            using (var first = pool.Rent())
            {
                first.Value.Append("customer-123");
            }

            using var second = pool.Rent();
            return new { pool.RetainedCount, second.Value.Length };
        })
        .Then("the same retained instance is available in clean state", result =>
        {
            ScenarioExpect.Equal(0, result.RetainedCount);
            ScenarioExpect.Equal(0, result.Length);
        })
        .AssertPassed();

    [Scenario("Retention predicate discards unhealthy items")]
    [Fact]
    public Task Retention_Predicate_Discards_Unhealthy_Items()
        => Given("an object pool with validation", () => ObjectPool<PooledBuffer>.Create()
            .WithFactory(static () => new PooledBuffer())
            .RetainWhen(static buffer => buffer.Length < 8)
            .Build())
        .When("returning an item that violates the predicate", pool =>
        {
            using (var lease = pool.Rent())
            {
                lease.Value.Append("oversized");
            }

            return pool.RetainedCount;
        })
        .Then("the item is not retained", retained => ScenarioExpect.Equal(0, retained))
        .AssertPassed();

    [Scenario("Max retained prevents unbounded growth")]
    [Fact]
    public Task Max_Retained_Prevents_Unbounded_Growth()
        => Given("an object pool with a two item retention limit", () => ObjectPool<PooledBuffer>.Create()
            .WithFactory(static () => new PooledBuffer())
            .WithMaxRetained(2)
            .Build())
        .When("returning three rented items", pool =>
        {
            var first = pool.Rent();
            var second = pool.Rent();
            var third = pool.Rent();
            first.Dispose();
            second.Dispose();
            third.Dispose();
            return pool.RetainedCount;
        })
        .Then("only the configured number of items is retained", retained => ScenarioExpect.Equal(2, retained))
        .AssertPassed();

    [Scenario("Disposed pool rejects future rents")]
    [Fact]
    public Task Disposed_Pool_Rejects_Future_Rents()
        => Given("a disposed object pool", () =>
        {
            var pool = ObjectPool<PooledBuffer>.Create().Build();
            pool.Dispose();
            return pool;
        })
        .Then("renting fails", pool => ScenarioExpect.Throws<ObjectDisposedException>(() => pool.Rent()))
        .AssertPassed();

    [Scenario("Invalid builder configuration is rejected")]
    [Fact]
    public Task Invalid_Builder_Configuration_Is_Rejected()
        => Given("an object pool builder", ObjectPool<PooledBuffer>.Create)
        .Then("invalid callbacks and limits fail fast", builder =>
        {
            ScenarioExpect.Throws<ArgumentNullException>(() => builder.WithFactory(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => builder.OnRent(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => builder.OnReturn(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => builder.RetainWhen(null!));
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => builder.WithMaxRetained(-1));
        })
        .AssertPassed();

    private sealed class PooledBuffer
    {
        private readonly List<string> _segments = [];

        public int Length => _segments.Sum(static segment => segment.Length);

        public void Append(string segment) => _segments.Add(segment);

        public void Reset() => _segments.Clear();
    }
}
