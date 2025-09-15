using PatternKit.Behavioral.Strategy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Strategy;

[Feature("AsyncStrategy<TIn,TOut> (first-match-wins async strategy)")]
public sealed class AsyncStrategyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Scenario context
    private sealed record Ctx(
        AsyncStrategy<int, string> S,
        List<string> Log,
        string? Result = null,
        Exception? Ex = null
    );

    // ---------- Helpers ----------
    private static Ctx Build_Defaulted()
    {
        var log = new List<string>();
        var s = AsyncStrategy<int, string>.Create()
            .When(static (n, _) => new ValueTask<bool>(n > 0))
                .Then((n, _) =>
                {
                    log.Add("pos");
                    return new ValueTask<string>("+" + n);
                })
            .When(static (n, _) => new ValueTask<bool>(n < 0))
                .Then((n, _) =>
                {
                    log.Add("neg");
                    return new ValueTask<string>(n.ToString());
                })
            .Default(static (_, _) => new ValueTask<string>("zero"))
            .Build();

        return new Ctx(s, log);
    }

    private static Ctx Build_NoDefault()
    {
        var log = new List<string>();
        var s = AsyncStrategy<int, string>.Create()
            .When(static (n, _) => new ValueTask<bool>(n > 0))
                .Then((n, _) =>
                {
                    log.Add("pos");
                    return new ValueTask<string>("+" + n);
                })
            .Build();

        return new Ctx(s, log);
    }

    private static Ctx Build_SyncAdapters()
    {
        // Mix synchronous adapters for predicate and default
        var s = AsyncStrategy<int, string>.Create()
            .When(static n => n % 2 == 0)
                .Then(static (_, _) => new ValueTask<string>("even"))
            .When(static (n, _) => new ValueTask<bool>(n >= 0))
                .Then(static (_, _) => new ValueTask<string>("nonneg"))
            .Default(static _ => "other")
            .Build();

        return new Ctx(s, new List<string>());
    }

    private static Ctx Build_OrderLog()
    {
        var log = new List<string>();
        var s = AsyncStrategy<int, string>.Create()
            .When(static (n, _) => new ValueTask<bool>(n >= 0))
                .Then((_, _) =>
                {
                    log.Add("first");
                    return new ValueTask<string>("first");
                })
            .When(static (n, _) => new ValueTask<bool>(n >= 0))
                .Then((_, _) =>
                {
                    log.Add("second");
                    return new ValueTask<string>("second");
                })
            .Default(static (_, _) => new ValueTask<string>("default"))
            .Build();

        return new Ctx(s, log);
    }

    private static Ctx Build_Cancellable()
    {
        var s = AsyncStrategy<int, string>.Create()
            .When(static (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return new ValueTask<bool>(true);
            })
            .Then(static (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return new ValueTask<string>("ok");
            })
            .Build();

        return new Ctx(s, new List<string>());
    }

    private static async Task<Ctx> ExecAsync(Ctx c, int n, CancellationToken ct = default)
    {
        var r = await c.S.ExecuteAsync(n, ct);
        return c with { Result = r, Ex = null };
    }

    private static async Task<Ctx> ExecCatchAsync(Ctx c, int n, CancellationToken ct = default)
    {
        try
        {
            var r = await c.S.ExecuteAsync(n, ct);
            return c with { Result = r, Ex = null };
        }
        catch (Exception ex)
        {
            return c with { Ex = ex };
        }
    }

    // ---------- Scenarios ----------

    [Scenario("First matching async branch runs; default used when none match")]
    [Fact]
    public async Task FirstMatchAndDefault()
    {
        await Given("a strategy with >0, <0, and default branches (async)", Build_Defaulted)
            .When("executing with 5", c => ExecAsync(c, 5))
            .Then("returns +5 and logs 'pos'", c => c.Result == "+5" && string.Join("|", c.Log) == "pos")
            .When("executing with -3", c => { c.Log.Clear(); return ExecAsync(c, -3); })
            .Then("returns -3 and logs 'neg'", c => c.Result == "-3" && string.Join("|", c.Log) == "neg")
            .When("executing with 0", c => { c.Log.Clear(); return ExecAsync(c, 0); })
            .Then("returns 'zero' and no branch logs", c => c.Result == "zero" && c.Log.Count == 0)
            .AssertPassed();
    }

    [Scenario("ExecuteAsync throws when nothing matches and no default is configured")]
    [Fact]
    public async Task ThrowsWithoutDefault()
    {
        await Given("a strategy without a default branch", Build_NoDefault)
            .When("executing with 0 (no predicate matches)", c => ExecCatchAsync(c, 0))
            .Then("captures InvalidOperationException", c => c.Ex is InvalidOperationException)
            .AssertPassed();
    }

    [Scenario("Synchronous adapters (When/Default) behave correctly")]
    [Fact]
    public async Task SyncAdaptersWork()
    {
        await Given("a strategy using sync adapters", Build_SyncAdapters)
            .When("executing with 2", c => ExecAsync(c, 2))
            .Then("returns 'even'", c => c.Result == "even")
            .When("executing with 1", c => ExecAsync(c, 1))
            .Then("returns 'nonneg'", c => c.Result == "nonneg")
            .When("executing with -1", c => ExecAsync(c, -1))
            .Then("returns 'other' via sync default", c => c.Result == "other")
            .AssertPassed();
    }

    [Scenario("Registration order preserved; only first matching handler runs")]
    [Fact]
    public async Task OrderPreserved_FirstMatchOnly()
    {
        await Given("a strategy with overlapping predicates in order", Build_OrderLog)
            .When("executing with 7 (both predicates true)", c => ExecAsync(c, 7))
            .Then("result comes from the first", c => c.Result == "first")
            .And("only 'first' logged", c => string.Join("|", c.Log) == "first")
            .AssertPassed();
    }

    [Scenario("Cancellation token is honored by predicates/handlers")]
    [Fact]
    public async Task CancellationPropagates()
    {
        await Given("a strategy whose predicate/handler check the token", Build_Cancellable)
            .When("executing with a cancelled token", c =>
            {
                var cts = new CancellationTokenSource();
                cts.Cancel();
                return ExecCatchAsync(c, 1, cts.Token);
            })
            .Then("captures OperationCanceledException", c => c.Ex is OperationCanceledException)
            .AssertPassed();
    }
}
