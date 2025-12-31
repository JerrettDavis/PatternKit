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
