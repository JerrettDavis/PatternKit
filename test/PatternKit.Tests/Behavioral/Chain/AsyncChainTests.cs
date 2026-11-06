using PatternKit.Behavioral.Chain;
using TinyBDD;

namespace PatternKit.Tests.Behavioral.Chain;

public sealed class AsyncChainTests
{
    #region AsyncActionChain<TContext> Tests

    [Scenario("AsyncActionChain Executes All Handlers")]
    [Fact]
    public async Task AsyncActionChain_Executes_All_Handlers()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .Use(async (ctx, ct, next) =>
            {
                log.Add($"h1-{ctx}");
                await next(ctx, ct);
            })
            .Use(async (ctx, ct, next) =>
            {
                log.Add($"h2-{ctx}");
                await next(ctx, ct);
            })
            .Build();

        await chain.ExecuteAsync(5);

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("h1-5", log[0]);
        ScenarioExpect.Equal("h2-5", log[1]);
    }

    [Scenario("AsyncActionChain When ThenContinue Conditional True")]
    [Fact]
    public async Task AsyncActionChain_When_ThenContinue_Conditional_True()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .When(x => x > 0)
            .ThenContinue(x => log.Add("positive"))
            .Finally(x => log.Add("always"))
            .Build();

        await chain.ExecuteAsync(5);

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("positive", log[0]);
        ScenarioExpect.Equal("always", log[1]);
    }

    [Scenario("AsyncActionChain When ThenContinue Conditional False")]
    [Fact]
    public async Task AsyncActionChain_When_ThenContinue_Conditional_False()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .When(x => x > 0)
            .ThenContinue(x => log.Add("positive"))
            .Finally(x => log.Add("always"))
            .Build();

        await chain.ExecuteAsync(-5);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("always", log[0]);
    }

    [Scenario("AsyncActionChain ThenStop Halts Chain")]
    [Fact]
    public async Task AsyncActionChain_ThenStop_Halts_Chain()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .When(x => x > 10)
            .ThenStop(x => log.Add("stopped"))
            .Finally(x => log.Add("finally"))
            .Build();

        await chain.ExecuteAsync(15);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("stopped", log[0]);
    }

    [Scenario("AsyncActionChain ThenStop Continues When False")]
    [Fact]
    public async Task AsyncActionChain_ThenStop_Continues_When_False()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .When(x => x > 10)
            .ThenStop(x => log.Add("stopped"))
            .Finally(x => log.Add("finally"))
            .Build();

        await chain.ExecuteAsync(5);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("finally", log[0]);
    }

    [Scenario("AsyncActionChain Handler Can ShortCircuit")]
    [Fact]
    public async Task AsyncActionChain_Handler_Can_ShortCircuit()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .Use(async (ctx, ct, next) =>
            {
                log.Add("handler-1");
                // Don't call next - short circuit
            })
            .Use(async (ctx, ct, next) =>
            {
                log.Add("handler-2");
                await next(ctx, ct);
            })
            .Build();

        await chain.ExecuteAsync(5);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("handler-1", log[0]);
    }

    [Scenario("AsyncActionChain Finally Executes At End")]
    [Fact]
    public async Task AsyncActionChain_Finally_Executes_At_End()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .Use(async (ctx, ct, next) =>
            {
                log.Add("handler");
                await next(ctx, ct);
            })
            .Finally(ctx => log.Add("finally"))
            .Build();

        await chain.ExecuteAsync(5);

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("handler", log[0]);
        ScenarioExpect.Equal("finally", log[1]);
    }

    [Scenario("AsyncActionChain Async Predicate")]
    [Fact]
    public async Task AsyncActionChain_Async_Predicate()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .When(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x > 0;
            })
            .ThenContinue(ctx => log.Add("positive"))
            .Build();

        await chain.ExecuteAsync(5);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("positive", log[0]);
    }

    [Scenario("AsyncActionChain Async Action")]
    [Fact]
    public async Task AsyncActionChain_Async_Action()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .When(x => x > 0)
            .ThenContinue(async (ctx, ct) =>
            {
                await Task.Delay(1, ct);
                log.Add("async-positive");
            })
            .Build();

        await chain.ExecuteAsync(5);

        ScenarioExpect.Single(log);
        ScenarioExpect.Equal("async-positive", log[0]);
    }

    [Scenario("AsyncActionChain Empty Chain Executes")]
    [Fact]
    public async Task AsyncActionChain_Empty_Chain_Executes()
    {
        var chain = AsyncActionChain<int>.Create().Build();

        await chain.ExecuteAsync(5); // Should not throw

        ScenarioExpect.True(true);
    }

    [Scenario("AsyncActionChain Context Passed Through")]
    [Fact]
    public async Task AsyncActionChain_Context_Passed_Through()
    {
        var capturedValues = new List<int>();
        var chain = AsyncActionChain<int>.Create()
            .Use(async (ctx, ct, next) =>
            {
                capturedValues.Add(ctx);
                await next(ctx + 10, ct);
            })
            .Use(async (ctx, ct, next) =>
            {
                capturedValues.Add(ctx);
                await next(ctx, ct);
            })
            .Build();

        await chain.ExecuteAsync(5);

        ScenarioExpect.Equal(5, capturedValues[0]);
        ScenarioExpect.Equal(15, capturedValues[1]);
    }

    [Scenario("AsyncActionChain Multiple When Clauses")]
    [Fact]
    public async Task AsyncActionChain_Multiple_When_Clauses()
    {
        var log = new List<string>();
        var chain = AsyncActionChain<int>.Create()
            .When(x => x > 100)
            .ThenContinue(x => log.Add("large"))
            .When(x => x > 10)
            .ThenContinue(x => log.Add("medium"))
            .When(x => x > 0)
            .ThenContinue(x => log.Add("small"))
            .Finally(x => log.Add("finally"))
            .Build();

        await chain.ExecuteAsync(50);

        // All conditions that pass add their entries
        ScenarioExpect.Contains("medium", log);
        ScenarioExpect.Contains("small", log);
        ScenarioExpect.Contains("finally", log);
        ScenarioExpect.DoesNotContain("large", log);
    }

    #endregion

    #region AsyncResultChain<TContext, TResult> Tests

    [Scenario("AsyncResultChain Returns First Match")]
    [Fact]
    public async Task AsyncResultChain_Returns_First_Match()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x < 0).Then(x => "negative")
            .When(x => x == 0).Then(x => "zero")
            .When(x => x > 0).Then(x => "positive")
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("positive", result);
    }

    [Scenario("AsyncResultChain Returns False When No Match")]
    [Fact]
    public async Task AsyncResultChain_Returns_False_When_No_Match()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x > 100).Then(x => "large")
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        ScenarioExpect.False(success);
        ScenarioExpect.Null(result);
    }

    [Scenario("AsyncResultChain Finally Fallback")]
    [Fact]
    public async Task AsyncResultChain_Finally_Fallback()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x < 0).Then(x => "negative")
            .Finally(x => "default")
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("default", result);
    }

    [Scenario("AsyncResultChain Async Predicate")]
    [Fact]
    public async Task AsyncResultChain_Async_Predicate()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x > 0;
            })
            .Then(x => "positive")
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("positive", result);
    }

    [Scenario("AsyncResultChain Async Producer")]
    [Fact]
    public async Task AsyncResultChain_Async_Producer()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x > 0)
            .Then(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return $"async-{x}";
            })
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("async-5", result);
    }

    [Scenario("AsyncResultChain Multiple Conditions")]
    [Fact]
    public async Task AsyncResultChain_Multiple_Conditions()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x >= 90).Then(_ => "A")
            .When(x => x >= 80).Then(_ => "B")
            .When(x => x >= 70).Then(_ => "C")
            .Finally(_ => "F")
            .Build();

        var (_, r1) = await chain.ExecuteAsync(95);
        var (_, r2) = await chain.ExecuteAsync(85);
        var (_, r3) = await chain.ExecuteAsync(75);
        var (_, r4) = await chain.ExecuteAsync(50);

        ScenarioExpect.Equal("A", r1);
        ScenarioExpect.Equal("B", r2);
        ScenarioExpect.Equal("C", r3);
        ScenarioExpect.Equal("F", r4);
    }

    [Scenario("AsyncResultChain Use Raw Handler")]
    [Fact]
    public async Task AsyncResultChain_Use_Raw_Handler()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .Use(async (x, ct) =>
            {
                if (x > 0)
                    return (true, $"positive-{x}");
                return (false, null);
            })
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("positive-5", result);
    }

    [Scenario("AsyncResultChain Finally Async Producer")]
    [Fact]
    public async Task AsyncResultChain_Finally_Async_Producer()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x > 100).Then(x => "large")
            .Finally(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return $"fallback-{x}";
            })
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("fallback-5", result);
    }

    [Scenario("AsyncResultChain Empty Chain Returns False")]
    [Fact]
    public async Task AsyncResultChain_Empty_Chain_Returns_False()
    {
        var chain = AsyncResultChain<int, string>.Create().Build();

        var (success, result) = await chain.ExecuteAsync(5);

        ScenarioExpect.False(success);
        ScenarioExpect.Null(result);
    }

    #endregion
}
