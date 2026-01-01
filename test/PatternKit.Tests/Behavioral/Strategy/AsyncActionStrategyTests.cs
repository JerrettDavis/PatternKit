using PatternKit.Behavioral.Strategy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Strategy;

[Feature("AsyncActionStrategy<TIn> (first-match-wins async action strategy)")]
public sealed class AsyncActionStrategyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Ctx(
        AsyncActionStrategy<int> S,
        List<string> Log,
        bool? Executed = null,
        Exception? Ex = null
    );

    private static Ctx Build_Defaulted()
    {
        var log = new List<string>();
        var s = AsyncActionStrategy<int>.Create()
            .When(static (n, _) => new ValueTask<bool>(n > 0))
                .Then((n, _) =>
                {
                    log.Add("pos:" + n);
                    return default;
                })
            .When(static (n, _) => new ValueTask<bool>(n < 0))
                .Then((n, _) =>
                {
                    log.Add("neg:" + n);
                    return default;
                })
            .Default((_, _) =>
            {
                log.Add("zero");
                return default;
            })
            .Build();

        return new Ctx(s, log);
    }

    private static Ctx Build_NoDefault()
    {
        var log = new List<string>();
        var s = AsyncActionStrategy<int>.Create()
            .When(static (n, _) => new ValueTask<bool>(n > 0))
                .Then((n, _) =>
                {
                    log.Add("pos:" + n);
                    return default;
                })
            .Build();

        return new Ctx(s, log);
    }

    private static Ctx Build_SyncAdapters()
    {
        var log = new List<string>();
        var s = AsyncActionStrategy<int>.Create()
            .When(static n => n % 2 == 0)
                .Then((n, _) =>
                {
                    log.Add("even:" + n);
                    return default;
                })
            .Default(n =>
            {
                log.Add("other:" + n);
            })
            .Build();

        return new Ctx(s, log);
    }

    private static async Task<Ctx> ExecAsync(Ctx c, int n, CancellationToken ct = default)
    {
        await c.S.ExecuteAsync(n, ct);
        return c with { Executed = true };
    }

    private static async Task<Ctx> TryExecAsync(Ctx c, int n, CancellationToken ct = default)
    {
        var result = await c.S.TryExecuteAsync(n, ct);
        return c with { Executed = result };
    }

    private static async Task<Ctx> ExecCatchAsync(Ctx c, int n, CancellationToken ct = default)
    {
        try
        {
            await c.S.ExecuteAsync(n, ct);
            return c with { Executed = true };
        }
        catch (Exception ex)
        {
            return c with { Ex = ex };
        }
    }

    [Scenario("First matching async branch runs the action")]
    [Fact]
    public async Task FirstMatchExecutesAction()
    {
        await Given("a strategy with >0, <0, and default branches", Build_Defaulted)
            .When("executing with 5", c => ExecAsync(c, 5))
            .Then("logs 'pos:5'", c => c.Log.Contains("pos:5"))
            .When("executing with -3", c => { c.Log.Clear(); return ExecAsync(c, -3); })
            .Then("logs 'neg:-3'", c => c.Log.Contains("neg:-3"))
            .When("executing with 0", c => { c.Log.Clear(); return ExecAsync(c, 0); })
            .Then("logs 'zero'", c => c.Log.Contains("zero"))
            .AssertPassed();
    }

    [Scenario("ExecuteAsync throws when nothing matches and no default")]
    [Fact]
    public async Task ThrowsWithoutDefault()
    {
        await Given("a strategy without a default branch", Build_NoDefault)
            .When("executing with 0 (no predicate matches)", c => ExecCatchAsync(c, 0))
            .Then("captures InvalidOperationException", c => c.Ex is InvalidOperationException)
            .AssertPassed();
    }

    [Scenario("TryExecuteAsync returns false when no match")]
    [Fact]
    public async Task TryExecuteReturnsFalse()
    {
        await Given("a strategy without a default branch", Build_NoDefault)
            .When("try-executing with 0", c => TryExecAsync(c, 0))
            .Then("returns false", c => c.Executed == false)
            .And("no exceptions thrown", c => c.Ex is null)
            .AssertPassed();
    }

    [Scenario("Synchronous adapters work correctly")]
    [Fact]
    public async Task SyncAdaptersWork()
    {
        await Given("a strategy using sync adapters", Build_SyncAdapters)
            .When("executing with 2", c => ExecAsync(c, 2))
            .Then("logs 'even:2'", c => c.Log.Contains("even:2"))
            .When("executing with 3", c => { c.Log.Clear(); return ExecAsync(c, 3); })
            .Then("logs 'other:3' via sync default", c => c.Log.Contains("other:3"))
            .AssertPassed();
    }
}

#region Additional AsyncActionStrategy Tests

public sealed class AsyncActionStrategyBuilderTests
{
    [Fact]
    public async Task TryExecuteAsync_WithDefault_ReturnsTrue()
    {
        var log = new List<string>();
        var strategy = AsyncActionStrategy<int>.Create()
            .When(n => n > 0).Then(_ => log.Add("positive"))
            .Default(_ => log.Add("default"))
            .Build();

        var result = await strategy.TryExecuteAsync(0);

        Assert.True(result);
        Assert.Contains("default", log);
    }

    [Fact]
    public async Task TryExecuteAsync_MatchingPredicate_ReturnsTrue()
    {
        var log = new List<string>();
        var strategy = AsyncActionStrategy<int>.Create()
            .When(n => n > 0).Then(_ => log.Add("positive"))
            .Build();

        var result = await strategy.TryExecuteAsync(5);

        Assert.True(result);
        Assert.Contains("positive", log);
    }

    [Fact]
    public async Task ExecuteAsync_Respects_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        var started = false;
        var strategy = AsyncActionStrategy<int>.Create()
            .When(async (n, ct) =>
            {
                started = true;
                ct.ThrowIfCancellationRequested();
                return n > 0;
            })
            .Then((n, ct) => default)
            .Build();

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await strategy.ExecuteAsync(5, cts.Token));

        Assert.True(started);
    }

    [Fact]
    public async Task SyncPredicate_WithCancellationToken_Works()
    {
        var tokenReceived = false;
        using var cts = new CancellationTokenSource();

        var strategy = AsyncActionStrategy<int>.Create()
            .When((n, ct) =>
            {
                tokenReceived = ct == cts.Token;
                return n > 0;
            })
            .Then((n, ct) => default)
            .Build();

        await strategy.ExecuteAsync(5, cts.Token);

        Assert.True(tokenReceived);
    }

    [Fact]
    public async Task Multiple_When_Branches_FirstMatch_Wins()
    {
        var log = new List<string>();
        var strategy = AsyncActionStrategy<int>.Create()
            .When(n => n % 2 == 0).Then(_ => log.Add("even"))
            .When(n => n % 3 == 0).Then(_ => log.Add("div3"))
            .When(n => n > 0).Then(_ => log.Add("positive"))
            .Build();

        await strategy.ExecuteAsync(6); // even and div3, but even wins

        Assert.Single(log);
        Assert.Equal("even", log[0]);
    }

    [Fact]
    public async Task Async_Handler_Executes_Fully()
    {
        var completed = false;
        var strategy = AsyncActionStrategy<int>.Create()
            .When((n, ct) => new ValueTask<bool>(true))
            .Then(async (n, ct) =>
            {
                await Task.Delay(10, ct);
                completed = true;
            })
            .Build();

        await strategy.ExecuteAsync(1);

        Assert.True(completed);
    }

    [Fact]
    public async Task Empty_Strategy_WithDefault_UsesDefault()
    {
        var log = new List<string>();
        var strategy = AsyncActionStrategy<int>.Create()
            .Default(_ => log.Add("default"))
            .Build();

        await strategy.ExecuteAsync(42);

        Assert.Single(log);
        Assert.Equal("default", log[0]);
    }

    [Fact]
    public async Task Empty_Strategy_NoDefault_Throws()
    {
        var strategy = AsyncActionStrategy<int>.Create().Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.ExecuteAsync(42).AsTask());
    }

    [Fact]
    public async Task Sync_Then_Handler_Works()
    {
        var log = new List<string>();
        var strategy = AsyncActionStrategy<int>.Create()
            .When(n => n > 0).Then(n => log.Add($"value:{n}"))
            .Build();

        await strategy.ExecuteAsync(42);

        Assert.Contains("value:42", log);
    }

    [Fact]
    public async Task Multiple_Strategies_Independent()
    {
        var log1 = new List<string>();
        var log2 = new List<string>();

        var s1 = AsyncActionStrategy<int>.Create()
            .When(n => n > 0).Then(_ => log1.Add("s1"))
            .Build();

        var s2 = AsyncActionStrategy<int>.Create()
            .When(n => n > 0).Then(_ => log2.Add("s2"))
            .Build();

        await s1.ExecuteAsync(1);
        await s2.ExecuteAsync(1);

        Assert.Single(log1);
        Assert.Single(log2);
        Assert.Equal("s1", log1[0]);
        Assert.Equal("s2", log2[0]);
    }

    [Fact]
    public async Task Concurrent_Execution_Safe()
    {
        var counter = 0;
        var strategy = AsyncActionStrategy<int>.Create()
            .When(n => true)
            .Then(async (n, ct) =>
            {
                await Task.Yield();
                Interlocked.Increment(ref counter);
            })
            .Build();

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => strategy.ExecuteAsync(1).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(100, counter);
    }
}

#endregion
