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