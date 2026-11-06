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

    [Scenario("Retention predicate disposes discarded disposable items")]
    [Fact]
    public Task Retention_Predicate_Disposes_Discarded_Disposable_Items()
        => Given("an object pool with disposable validation", () => ObjectPool<DisposableBuffer>.Create()
            .WithFactory(static () => new DisposableBuffer())
            .RetainWhen(static _ => false)
            .Build())
        .When("returning an item rejected by the predicate", pool =>
        {
            var lease = pool.Rent();
            var item = lease.Value;
            lease.Dispose();
            return new { pool.RetainedCount, item.IsDisposed };
        })
        .Then("the item is disposed instead of retained", result =>
        {
            ScenarioExpect.Equal(0, result.RetainedCount);
            ScenarioExpect.True(result.IsDisposed);
        })
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

    [Scenario("Max retained disposes disposable items above capacity")]
    [Fact]
    public Task Max_Retained_Disposes_Disposable_Items_Above_Capacity()
        => Given("an object pool with a one item retention limit", () => ObjectPool<DisposableBuffer>.Create()
            .WithFactory(static () => new DisposableBuffer())
            .WithMaxRetained(1)
            .Build())
        .When("returning more disposable items than the pool can retain", pool =>
        {
            var first = pool.Rent();
            var second = pool.Rent();
            var overflow = second.Value;
            first.Dispose();
            second.Dispose();
            return new { pool.RetainedCount, overflow.IsDisposed };
        })
        .Then("only one item is retained and the overflow is disposed", result =>
        {
            ScenarioExpect.Equal(1, result.RetainedCount);
            ScenarioExpect.True(result.IsDisposed);
        })
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

    [Scenario("Disposing a pool releases retained disposable items")]
    [Fact]
    public Task Disposing_A_Pool_Releases_Retained_Disposable_Items()
        => Given("an object pool with a returned disposable item", () =>
        {
            var pool = ObjectPool<DisposableBuffer>.Create()
                .WithFactory(static () => new DisposableBuffer())
                .WithMaxRetained(1)
                .Build();
            var lease = pool.Rent();
            var item = lease.Value;
            lease.Dispose();
            return new { Pool = pool, Item = item };
        })
        .When("the pool is disposed", ctx =>
        {
            ctx.Pool.Dispose();
            return new { ctx.Pool.RetainedCount, ctx.Item.IsDisposed };
        })
        .Then("the retained item is disposed and removed", result =>
        {
            ScenarioExpect.Equal(0, result.RetainedCount);
            ScenarioExpect.True(result.IsDisposed);
        })
        .AssertPassed();

    [Scenario("Returning a leased item after dispose releases the item")]
    [Fact]
    public Task Returning_A_Leased_Item_After_Dispose_Releases_The_Item()
        => Given("an object pool with a rented disposable item", () =>
        {
            var pool = ObjectPool<DisposableBuffer>.Create()
                .WithFactory(static () => new DisposableBuffer())
                .WithMaxRetained(1)
                .Build();
            var lease = pool.Rent();
            return new { Pool = pool, Lease = lease, Item = lease.Value };
        })
        .When("the pool is disposed before the lease is returned", ctx =>
        {
            ctx.Pool.Dispose();
            ctx.Lease.Dispose();
            return new { ctx.Pool.RetainedCount, ctx.Item.IsDisposed };
        })
        .Then("the returned item is disposed instead of retained", result =>
        {
            ScenarioExpect.Equal(0, result.RetainedCount);
            ScenarioExpect.True(result.IsDisposed);
        })
        .AssertPassed();

    [Scenario("Returning a leased item after dispose skips throwing callbacks")]
    [Fact]
    public Task Returning_A_Leased_Item_After_Dispose_Skips_Throwing_Callbacks()
        => Given("an object pool with a throwing return callback and a rented disposable item", () =>
        {
            var pool = ObjectPool<DisposableBuffer>.Create()
                .WithFactory(static () => new DisposableBuffer())
                .OnReturn(static _ => throw new InvalidOperationException("return callback should not run"))
                .RetainWhen(static _ => throw new InvalidOperationException("retention callback should not run"))
                .WithMaxRetained(1)
                .Build();
            var lease = pool.Rent();
            return new { Pool = pool, Lease = lease, Item = lease.Value };
        })
        .When("the pool is disposed before the lease is returned", ctx =>
        {
            ctx.Pool.Dispose();
            ctx.Lease.Dispose();
            return new { ctx.Pool.RetainedCount, ctx.Item.IsDisposed };
        })
        .Then("the callbacks are skipped and the returned item is disposed", result =>
        {
            ScenarioExpect.Equal(0, result.RetainedCount);
            ScenarioExpect.True(result.IsDisposed);
        })
        .AssertPassed();

    [Scenario("Throwing return callbacks dispose the item before rethrowing")]
    [Fact]
    public Task Throwing_Return_Callbacks_Dispose_The_Item_Before_Rethrowing()
        => Given("an object pool with a throwing return callback and a rented disposable item", () =>
        {
            var pool = ObjectPool<DisposableBuffer>.Create()
                .WithFactory(static () => new DisposableBuffer())
                .OnReturn(static _ => throw new InvalidOperationException("return callback failed"))
                .WithMaxRetained(1)
                .Build();
            var lease = pool.Rent();
            return new { Lease = lease, Item = lease.Value };
        })
        .When("returning the lease", ctx =>
        {
            ScenarioExpect.Throws<InvalidOperationException>(() => ctx.Lease.Dispose());
            return ctx.Item.IsDisposed;
        })
        .Then("the item is disposed before the callback failure is rethrown", disposed => ScenarioExpect.True(disposed))
        .AssertPassed();

    [Scenario("Disposing during item creation releases the created item")]
    [Fact]
    public Task Disposing_During_Item_Creation_Releases_The_Created_Item()
        => Given("an object pool that is disposed by its factory", () =>
        {
            ObjectPool<DisposableBuffer>? pool = null;
            DisposableBuffer? item = null;
            pool = ObjectPool<DisposableBuffer>.Create()
                .WithFactory(() =>
                {
                    item = new DisposableBuffer();
                    pool!.Dispose();
                    return item;
                })
                .Build();

            return new { Pool = pool, Item = new Func<DisposableBuffer?>(() => item) };
        })
        .When("renting causes the factory to dispose the pool", ctx =>
        {
            ScenarioExpect.Throws<ObjectDisposedException>(() => ctx.Pool.Rent());
            return ctx.Item();
        })
        .Then("the created item is disposed instead of leaked", item =>
        {
            ScenarioExpect.NotNull(item);
            ScenarioExpect.True(item!.IsDisposed);
        })
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

    private sealed class DisposableBuffer : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
