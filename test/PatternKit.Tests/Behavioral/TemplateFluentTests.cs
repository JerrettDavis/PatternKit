using System.Collections.Concurrent;
using PatternKit.Behavioral.Template;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - Template<TContext,TResult> (fluent skeleton with hooks)")]
public sealed class TemplateFluentTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Execute runs Before → Step → After in order")]
    [Fact]
    public Task Execute_RunsHooks_InOrder()
        => Given("a calls queue and a template", () =>
            {
                var calls = new ConcurrentQueue<string>();
                var tpl = Template<string, int>
                    .Create(ctx => { calls.Enqueue($"step:{ctx}"); return ctx.Length; })
                    .Before(ctx => calls.Enqueue($"before:{ctx}"))
                    .After((ctx, res) => calls.Enqueue($"after:{ctx}:{res}"))
                    .Build();
                return (tpl, calls);
            })
           .When("executed with 'abc'", x => (result: x.tpl.Execute("abc"), x.calls))
           .Then("returns 3", r => r.result == 3)
           .And("calls recorded in order", r =>
           {
               var arr = r.Item2.ToArray();
               return arr.Length == 3 && arr[0] == "before:abc" && arr[1] == "step:abc" && arr[2] == "after:abc:3";
           })
           .AssertPassed();

    [Scenario("TryExecute captures errors and invokes OnError")]
    [Fact]
    public Task TryExecute_Captures_Error()
        => Given("a throwing template and observer holder", () =>
            {
                var holder = new { Observed = new string?[] { null } };
                var tpl = Template<int, int>
                    .Create(_ => throw new InvalidOperationException("boom"))
                    .OnError((ctx, err) => holder.Observed[0] = $"ctx={ctx};err={err}")
                    .Build();
                return (tpl, holder);
            })
           .When("TryExecute with 42", x => { var ok = x.tpl.TryExecute(42, out var result, out var error); return (ok, result, error, x.holder); })
           .Then("returns false", r => !r.ok)
           .And("result is default", r => EqualityComparer<int>.Default.Equals(r.result, default))
           .And("error not null", r => r.error is { Length: > 0 })
           .And("OnError observed", r => r.holder.Observed[0] == "ctx=42;err=boom")
           .AssertPassed();

    [Scenario("Multiple Before/After hooks compose (multicast)")]
    [Fact]
    public Task Hooks_Compose_Multicast()
        => Given("counter holder and template", () =>
            {
                var counts = new int[2]; // [0]=before, [1]=after
                var tpl = Template<string, string>
                    .Create(ctx => ctx.ToUpperInvariant())
                    .Before(_ => Interlocked.Increment(ref counts[0]))
                    .Before(_ => Interlocked.Increment(ref counts[0]))
                    .After((_, _) => Interlocked.Increment(ref counts[1]))
                    .After((_, _) => Interlocked.Increment(ref counts[1]))
                    .Build();
                return (tpl, counts);
            })
           .When("executed concurrently twice", x => { Task.WaitAll(Task.Run(() => x.tpl.Execute("x")), Task.Run(() => x.tpl.Execute("y"))); return x.counts; })
           .Then("before called 4 times", counts => counts[0] == 4)
           .And("after called 4 times", counts => counts[1] == 4)
           .AssertPassed();

    [Scenario("Synchronized enforces mutual exclusion")]
    [Fact]
    public Task Synchronized_Enforces_Mutex()
        => Given("a synchronized template and concurrency holder", () =>
            {
                var holder = new int[2]; // [0]=concurrent, [1]=max
                var tpl = Template<int, int>
                    .Create(ctx =>
                    {
                        var c = Interlocked.Increment(ref holder[0]);
                        while (true)
                        {
                            var snap = Volatile.Read(ref holder[1]);
                            var next = Math.Max(snap, c);
                            if (Interlocked.CompareExchange(ref holder[1], next, snap) == snap) break;
                        }
                        Thread.Sleep(20);
                        Interlocked.Decrement(ref holder[0]);
                        return ctx * 2;
                    })
                    .Synchronized()
                    .Build();
                return (tpl, holder);
            })
           .When("executed on 8 tasks", x =>
           {
               var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => x.tpl.Execute(2))).ToArray();
               Task.WaitAll(tasks);
               return (results: tasks.Select(t => t.Result).ToArray(), x.holder);
           })
           .Then("all results are 4", r => r.results.All(v => v == 4))
           .And("max concurrency is 1", r => r.holder[1] == 1)
           .AssertPassed();
}

#region Additional Template Tests

public sealed class ActionTemplateTests
{
    [Fact]
    public void ActionTemplate_Execute_NoHooks()
    {
        var executed = false;
        var tpl = ActionTemplate<int>.Create(ctx => executed = true).Build();

        tpl.Execute(42);

        Assert.True(executed);
    }

    [Fact]
    public void ActionTemplate_Execute_WithBefore()
    {
        var log = new List<string>();
        var tpl = ActionTemplate<int>.Create(ctx => log.Add("step"))
            .Before(ctx => log.Add("before"))
            .Build();

        tpl.Execute(1);

        Assert.Equal(new[] { "before", "step" }, log);
    }

    [Fact]
    public void ActionTemplate_Execute_WithAfter()
    {
        var log = new List<string>();
        var tpl = ActionTemplate<int>.Create(ctx => log.Add("step"))
            .After(ctx => log.Add("after"))
            .Build();

        tpl.Execute(1);

        Assert.Equal(new[] { "step", "after" }, log);
    }

    [Fact]
    public void ActionTemplate_Execute_WithAllHooks()
    {
        var log = new List<string>();
        var tpl = ActionTemplate<int>.Create(ctx => log.Add($"step:{ctx}"))
            .Before(ctx => log.Add($"before:{ctx}"))
            .After(ctx => log.Add($"after:{ctx}"))
            .Build();

        tpl.Execute(42);

        Assert.Equal(new[] { "before:42", "step:42", "after:42" }, log);
    }

    [Fact]
    public void ActionTemplate_TryExecute_Success()
    {
        var executed = false;
        var tpl = ActionTemplate<int>.Create(ctx => executed = true).Build();

        var ok = tpl.TryExecute(42, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(executed);
    }

    [Fact]
    public void ActionTemplate_TryExecute_Error()
    {
        string? observedError = null;
        var tpl = ActionTemplate<int>.Create(_ => throw new InvalidOperationException("boom"))
            .OnError((ctx, err) => observedError = err)
            .Build();

        var ok = tpl.TryExecute(42, out var error);

        Assert.False(ok);
        Assert.Equal("boom", error);
        Assert.Equal("boom", observedError);
    }

    [Fact]
    public void ActionTemplate_Synchronized()
    {
        var concurrent = 0;
        var maxConcurrent = 0;
        var tpl = ActionTemplate<int>.Create(_ =>
        {
            var c = Interlocked.Increment(ref concurrent);
            maxConcurrent = Math.Max(maxConcurrent, c);
            Thread.Sleep(10);
            Interlocked.Decrement(ref concurrent);
        }).Synchronized().Build();

        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() => tpl.Execute(1))).ToArray();
        Task.WaitAll(tasks);

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public void ActionTemplate_MultipleHooks_Compose()
    {
        var count = 0;
        var tpl = ActionTemplate<int>.Create(_ => { })
            .Before(_ => count++)
            .Before(_ => count++)
            .After(_ => count++)
            .After(_ => count++)
            .OnError((_, _) => count++)
            .OnError((_, _) => count++)
            .Build();

        tpl.Execute(1);

        Assert.Equal(4, count); // 2 before + 2 after
    }
}

public sealed class AsyncTemplateTests
{
    [Fact]
    public async Task AsyncTemplate_Execute_Simple()
    {
        var tpl = AsyncTemplate<int, string>.Create(async (ctx, ct) =>
        {
            await Task.Yield();
            return ctx.ToString();
        }).Build();

        var result = await tpl.ExecuteAsync(42);

        Assert.Equal("42", result);
    }

    [Fact]
    public async Task AsyncTemplate_Execute_WithHooks()
    {
        var log = new List<string>();
        var tpl = AsyncTemplate<int, string>.Create(async (ctx, ct) =>
        {
            await Task.Yield();
            log.Add($"step:{ctx}");
            return ctx.ToString();
        })
        .Before((ctx, ct) => { log.Add($"before:{ctx}"); return default; })
        .After((ctx, res, ct) => { log.Add($"after:{ctx}:{res}"); return default; })
        .Build();

        var result = await tpl.ExecuteAsync(42);

        Assert.Equal("42", result);
        Assert.Equal(new[] { "before:42", "step:42", "after:42:42" }, log);
    }

    [Fact]
    public async Task AsyncTemplate_TryExecuteAsync_Success()
    {
        var tpl = AsyncTemplate<int, string>.Create(async (ctx, ct) =>
        {
            await Task.Yield();
            return ctx.ToString();
        }).Build();

        var (ok, result, error) = await tpl.TryExecuteAsync(42);

        Assert.True(ok);
        Assert.Equal("42", result);
        Assert.Null(error);
    }

    [Fact]
    public async Task AsyncTemplate_TryExecuteAsync_Error()
    {
        string? observedError = null;
        var tpl = AsyncTemplate<int, string>.Create(async (ctx, ct) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        })
        .OnError((ctx, err, ct) => { observedError = err; return default; })
        .Build();

        var (ok, result, error) = await tpl.TryExecuteAsync(42);

        Assert.False(ok);
        Assert.Equal("boom", error);
        Assert.Equal("boom", observedError);
    }

    [Fact]
    public async Task AsyncTemplate_Synchronized()
    {
        var concurrent = 0;
        var maxConcurrent = 0;
        var tpl = AsyncTemplate<int, int>.Create(async (ctx, ct) =>
        {
            var c = Interlocked.Increment(ref concurrent);
            maxConcurrent = Math.Max(maxConcurrent, c);
            await Task.Delay(10, ct);
            Interlocked.Decrement(ref concurrent);
            return ctx * 2;
        }).Synchronized().Build();

        var tasks = Enumerable.Range(0, 4).Select(_ => tpl.ExecuteAsync(1)).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrent);
    }
}

public sealed class AsyncActionTemplateTests
{
    [Fact]
    public async Task AsyncActionTemplate_Execute_Simple()
    {
        var executed = false;
        var tpl = AsyncActionTemplate<int>.Create(async (ctx, ct) =>
        {
            await Task.Yield();
            executed = true;
        }).Build();

        await tpl.ExecuteAsync(42);

        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionTemplate_Execute_WithHooks()
    {
        var log = new List<string>();
        var tpl = AsyncActionTemplate<int>.Create(async (ctx, ct) =>
        {
            await Task.Yield();
            log.Add($"step:{ctx}");
        })
        .Before((ctx, ct) => { log.Add($"before:{ctx}"); return default; })
        .After((ctx, ct) => { log.Add($"after:{ctx}"); return default; })
        .Build();

        await tpl.ExecuteAsync(42);

        Assert.Equal(new[] { "before:42", "step:42", "after:42" }, log);
    }

    [Fact]
    public async Task AsyncActionTemplate_TryExecuteAsync_Success()
    {
        var executed = false;
        var tpl = AsyncActionTemplate<int>.Create(async (ctx, ct) =>
        {
            await Task.Yield();
            executed = true;
        }).Build();

        var (ok, error) = await tpl.TryExecuteAsync(42);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionTemplate_TryExecuteAsync_Error()
    {
        string? observedError = null;
        var tpl = AsyncActionTemplate<int>.Create(async (ctx, ct) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        })
        .OnError((ctx, err, ct) => { observedError = err; return default; })
        .Build();

        var (ok, error) = await tpl.TryExecuteAsync(42);

        Assert.False(ok);
        Assert.Equal("boom", error);
        Assert.Equal("boom", observedError);
    }

    [Fact]
    public async Task AsyncActionTemplate_Synchronized()
    {
        var concurrent = 0;
        var maxConcurrent = 0;
        var tpl = AsyncActionTemplate<int>.Create(async (ctx, ct) =>
        {
            var c = Interlocked.Increment(ref concurrent);
            maxConcurrent = Math.Max(maxConcurrent, c);
            await Task.Delay(10, ct);
            Interlocked.Decrement(ref concurrent);
        }).Synchronized().Build();

        var tasks = Enumerable.Range(0, 4).Select(_ => tpl.ExecuteAsync(1)).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task AsyncActionTemplate_MultipleHooks_Compose()
    {
        var count = 0;
        var tpl = AsyncActionTemplate<int>.Create(async (ctx, ct) => await Task.Yield())
            .Before((_, _) => { Interlocked.Increment(ref count); return default; })
            .Before((_, _) => { Interlocked.Increment(ref count); return default; })
            .After((_, _) => { Interlocked.Increment(ref count); return default; })
            .After((_, _) => { Interlocked.Increment(ref count); return default; })
            .Build();

        await tpl.ExecuteAsync(1);

        Assert.Equal(4, count);
    }

    [Fact]
    public async Task AsyncActionTemplate_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        var tokenReceived = false;
        var tpl = AsyncActionTemplate<int>.Create((ctx, ct) =>
        {
            tokenReceived = ct == cts.Token;
            return default;
        }).Build();

        await tpl.ExecuteAsync(1, cts.Token);

        Assert.True(tokenReceived);
    }
}

public sealed class TemplateBuilderTests
{
    [Fact]
    public void Template_Create_NullStep_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Template<int, int>.Create(null!));
    }

    [Fact]
    public void Template_TryExecute_Synchronized_Success()
    {
        var tpl = Template<int, int>.Create(x => x * 2)
            .Synchronized()
            .Build();

        var ok = tpl.TryExecute(21, out var result, out var error);

        Assert.True(ok);
        Assert.Equal(42, result);
        Assert.Null(error);
    }

    [Fact]
    public void Template_TryExecute_Synchronized_Error()
    {
        var errorObserved = false;
        var tpl = Template<int, int>.Create(_ => throw new Exception("oops"))
            .Synchronized()
            .OnError((_, _) => errorObserved = true)
            .Build();

        var ok = tpl.TryExecute(1, out var result, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.True(errorObserved);
    }

    [Fact]
    public void ActionTemplate_Create_NullStep_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActionTemplate<int>.Create(null!));
    }

    [Fact]
    public void ActionTemplate_TryExecute_Synchronized_Success()
    {
        var executed = false;
        var tpl = ActionTemplate<int>.Create(_ => executed = true)
            .Synchronized()
            .Build();

        var ok = tpl.TryExecute(1, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(executed);
    }

    [Fact]
    public void ActionTemplate_TryExecute_Synchronized_Error()
    {
        var errorObserved = false;
        var tpl = ActionTemplate<int>.Create(_ => throw new Exception("oops"))
            .Synchronized()
            .OnError((_, _) => errorObserved = true)
            .Build();

        var ok = tpl.TryExecute(1, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.True(errorObserved);
    }
}

#endregion