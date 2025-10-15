using System.Collections.Concurrent;
using PatternKit.Behavioral.Template;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - AsyncTemplate<TContext,TResult> (fluent async skeleton with hooks)")]
public sealed class AsyncTemplateFluentTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("ExecuteAsync runs Before → Step → After in order")]
    [Fact]
    public Task ExecuteAsync_RunsHooks_InOrder()
        => Given("a calls queue and an async template", () =>
            {
                var calls = new ConcurrentQueue<string>();
                var tpl = AsyncTemplate<string, int>
                    .Create(async (ctx, ct) =>
                    {
                        await Task.Delay(5, ct);
                        calls.Enqueue($"step:{ctx}");
                        return ctx.Length;
                    })
                    .Before((ctx, _) =>
                    {
                        calls.Enqueue($"before:{ctx}");
                        return default;
                    })
                    .After((ctx, res, _) =>
                    {
                        calls.Enqueue($"after:{ctx}:{res}");
                        return default;
                    })
                    .Build();
                return (tpl, calls);
            })
            .When("executed with 'abc'",
                (Func<(AsyncTemplate<string, int> tpl, ConcurrentQueue<string> calls), ValueTask<(int result, ConcurrentQueue<string> calls)>>)(async x
                    =>
                {
                    var result = await x.tpl.ExecuteAsync("abc");
                    return (result, x.calls);
                }))
            .Then("returns 3", r => r.result == 3)
            .And("calls recorded in order", r =>
            {
                var arr = r.calls.ToArray();
                return arr is ["before:abc", "step:abc", "after:abc:3"];
            })
            .AssertPassed();

    [Scenario("TryExecuteAsync captures errors and invokes OnError")]
    [Fact]
    public Task TryExecuteAsync_Captures_Error()
        => Given("a throwing template and an observer holder", () =>
            {
                var holder = new string?[] { null };
                var tpl = AsyncTemplate<int, int>
                    .Create((_, _) => ValueTask.FromException<int>(new InvalidOperationException("boom")))
                    .OnError((ctx, err, _) =>
                    {
                        holder[0] = $"ctx={ctx};err={err}";
                        return default;
                    })
                    .Build();
                return (tpl, holder);
            })
            .When("TryExecuteAsync with 42",
                (Func<(AsyncTemplate<int, int> tpl, string?[] holder), ValueTask<(bool ok, int result, string? error, string?[] holder)>>)(async x =>
                {
                    var tuple = await x.tpl.TryExecuteAsync(42);
                    var res = tuple.result;
                    return (tuple.ok, res, tuple.error, x.holder);
                }))
            .Then("returns false", r => !r.ok)
            .And("error not null", r => r.error is { Length: > 0 })
            .And("OnError observed", r => r.holder[0] == "ctx=42;err=boom")
            .AssertPassed();

    [Scenario("Multiple Before/After hooks compose (multicast)")]
    [Fact]
    public Task Hooks_Compose_Multicast()
        => Given("counter holder and async template", () =>
            {
                var counts = new int[2]; // [0]=before, [1]=after
                var tpl = AsyncTemplate<string, string>
                    .Create((ctx, _) => ValueTask.FromResult(ctx.ToUpperInvariant()))
                    .Before((_, _) =>
                    {
                        Interlocked.Increment(ref counts[0]);
                        return default;
                    })
                    .Before((_, _) =>
                    {
                        Interlocked.Increment(ref counts[0]);
                        return default;
                    })
                    .After((_, _, _) =>
                    {
                        Interlocked.Increment(ref counts[1]);
                        return default;
                    })
                    .After((_, _, _) =>
                    {
                        Interlocked.Increment(ref counts[1]);
                        return default;
                    })
                    .Build();
                return (tpl, counts);
            })
            .When("run twice concurrently", (Func<(AsyncTemplate<string, string> tpl, int[] counts), ValueTask<int[]>>)(async x =>
            {
                var t1 = x.tpl.ExecuteAsync("x");
                var t2 = x.tpl.ExecuteAsync("y");
                await Task.WhenAll(t1, t2);
                return x.counts;
            }))
            .Then("before called 4 times", counts => counts[0] == 4)
            .And("after called 4 times", counts => counts[1] == 4)
            .AssertPassed();

    [Scenario("Synchronized enforces mutual exclusion")]
    [Fact]
    public Task Synchronized_Enforces_Mutex()
        => Given("a synchronized async template and concurrency holder", () =>
            {
                var holder = new int[2]; // [0]=concurrent, [1]=max
                var tpl = AsyncTemplate<int, int>
                    .Create(async (ctx, ct) =>
                    {
                        var c = Interlocked.Increment(ref holder[0]);
                        while (true)
                        {
                            var snap = Volatile.Read(ref holder[1]);
                            var next = Math.Max(snap, c);
                            if (Interlocked.CompareExchange(ref holder[1], next, snap) == snap) break;
                        }

                        await Task.Delay(20, ct);
                        Interlocked.Decrement(ref holder[0]);
                        return ctx * 2;
                    })
                    .Synchronized()
                    .Build();
                return (tpl, holder);
            })
            .When("executed on 8 tasks", (Func<(AsyncTemplate<int, int> tpl, int[] holder), ValueTask<(int[] results, int[] holder)>>)(async x =>
            {
                var tasks = Enumerable.Range(0, 8).Select(_ => x.tpl.ExecuteAsync(2)).ToArray();
                var results = await Task.WhenAll(tasks);
                return (results, x.holder);
            }))
            .Then("all results are 4", r => r.results.All(v => v == 4))
            .And("max concurrency is 1", r => r.holder[1] == 1)
            .AssertPassed();
}