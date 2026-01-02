using PatternKit.Behavioral.Chain;

namespace PatternKit.Tests.Behavioral.Chain;

public sealed class AsyncChainTests
{
    #region AsyncActionChain<TContext> Tests

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

        Assert.Equal(2, log.Count);
        Assert.Equal("h1-5", log[0]);
        Assert.Equal("h2-5", log[1]);
    }

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

        Assert.Equal(2, log.Count);
        Assert.Equal("positive", log[0]);
        Assert.Equal("always", log[1]);
    }

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

        Assert.Single(log);
        Assert.Equal("always", log[0]);
    }

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

        Assert.Single(log);
        Assert.Equal("stopped", log[0]);
    }

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

        Assert.Single(log);
        Assert.Equal("finally", log[0]);
    }

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

        Assert.Single(log);
        Assert.Equal("handler-1", log[0]);
    }

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

        Assert.Equal(2, log.Count);
        Assert.Equal("handler", log[0]);
        Assert.Equal("finally", log[1]);
    }

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

        Assert.Single(log);
        Assert.Equal("positive", log[0]);
    }

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

        Assert.Single(log);
        Assert.Equal("async-positive", log[0]);
    }

    [Fact]
    public async Task AsyncActionChain_Empty_Chain_Executes()
    {
        var chain = AsyncActionChain<int>.Create().Build();

        await chain.ExecuteAsync(5); // Should not throw

        Assert.True(true);
    }

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

        Assert.Equal(5, capturedValues[0]);
        Assert.Equal(15, capturedValues[1]);
    }

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
        Assert.Contains("medium", log);
        Assert.Contains("small", log);
        Assert.Contains("finally", log);
        Assert.DoesNotContain("large", log);
    }

    #endregion

    #region AsyncResultChain<TContext, TResult> Tests

    [Fact]
    public async Task AsyncResultChain_Returns_First_Match()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x < 0).Then(x => "negative")
            .When(x => x == 0).Then(x => "zero")
            .When(x => x > 0).Then(x => "positive")
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        Assert.True(success);
        Assert.Equal("positive", result);
    }

    [Fact]
    public async Task AsyncResultChain_Returns_False_When_No_Match()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x > 100).Then(x => "large")
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public async Task AsyncResultChain_Finally_Fallback()
    {
        var chain = AsyncResultChain<int, string>.Create()
            .When(x => x < 0).Then(x => "negative")
            .Finally(x => "default")
            .Build();

        var (success, result) = await chain.ExecuteAsync(5);

        Assert.True(success);
        Assert.Equal("default", result);
    }

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

        Assert.True(success);
        Assert.Equal("positive", result);
    }

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

        Assert.True(success);
        Assert.Equal("async-5", result);
    }

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

        Assert.Equal("A", r1);
        Assert.Equal("B", r2);
        Assert.Equal("C", r3);
        Assert.Equal("F", r4);
    }

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

        Assert.True(success);
        Assert.Equal("positive-5", result);
    }

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

        Assert.True(success);
        Assert.Equal("fallback-5", result);
    }

    [Fact]
    public async Task AsyncResultChain_Empty_Chain_Returns_False()
    {
        var chain = AsyncResultChain<int, string>.Create().Build();

        var (success, result) = await chain.ExecuteAsync(5);

        Assert.False(success);
        Assert.Null(result);
    }

    #endregion
}
