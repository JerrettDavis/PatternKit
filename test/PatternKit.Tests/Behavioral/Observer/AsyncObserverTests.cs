using PatternKit.Behavioral.Observer;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Observer;

[Feature("AsyncObserver<TEvent> (typed, fluent, thread-safe async event hub)")]
public sealed class AsyncObserverTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private readonly record struct Evt(int Id, string Name);

    private sealed record Ctx(AsyncObserver<Evt> Hub, List<string> Log)
    {
        public Ctx() : this(AsyncObserver<Evt>.Create().Build(), []) { }
    }

    private static Ctx Build_Default() => new();

    private static Ctx Build_With_Sink_Aggregate(List<(Exception ex, Evt evt)> sink)
        => new(
            AsyncObserver<Evt>.Create()
                .OnError((ex, e) => { sink.Add((ex, e)); return default; })
                .ThrowAggregate()
                .Build(),
            []);

    private static Ctx Build_With_Sink_Swallow(List<(Exception ex, Evt evt)> sink)
        => new(
            AsyncObserver<Evt>.Create()
                .OnError((ex, e) => { sink.Add((ex, e)); return default; })
                .SwallowErrors()
                .Build(),
            []);

    private static Ctx Build_With_Sink_ThrowFirst(List<(Exception ex, Evt evt)> sink)
        => new(
            AsyncObserver<Evt>.Create()
                .OnError((ex, e) => { sink.Add((ex, e)); return default; })
                .ThrowFirstError()
                .Build(),
            []);

    // Helpers
    private static ValueTask<Ctx> Sub_All_Async(Ctx c)
    {
        var log = c.Log;
        c.Hub.Subscribe(async e => { await Task.Yield(); log.Add($"all:{e.Id}:{e.Name}"); });
        return ValueTask.FromResult(c);
    }

    private static ValueTask<Ctx> Sub_Filtered_Async(Ctx c)
    {
        var log = c.Log;
        c.Hub.Subscribe(async e => { await Task.Yield(); return (e.Id % 2) == 0; },
                        async e => { await Task.Delay(1); log.Add($"even:{e.Id}"); });
        c.Hub.Subscribe(async e => { await Task.Yield(); return (e.Id % 2) == 1; },
                        async e => { await Task.Delay(1); log.Add($"odd:{e.Id}"); });
        return ValueTask.FromResult(c);
    }

    private static async ValueTask<Ctx> PublishAsync(Ctx c, int id, string name)
    {
        var ev = new Evt(id, name);
        await c.Hub.PublishAsync(ev);
        return c;
    }

    private static async ValueTask<Ctx> AddAllAndFiltered(Ctx c)
    {
        await Sub_All_Async(c);
        await Sub_Filtered_Async(c);
        return c;
    }

    private static async ValueTask<Ctx> PublishTwo(Ctx c, int id1, string n1, int id2, string n2)
    {
        await PublishAsync(c, id1, n1);
        await PublishAsync(c, id2, n2);
        return c;
    }

    private static async ValueTask<Ctx> PublishCatch<TEx>(Ctx c, Evt ev, string marker) where TEx : Exception
    {
        try { await c.Hub.PublishAsync(ev); }
        catch (TEx) { c.Log.Add(marker); }
        return c;
    }

    private static async ValueTask<Ctx> AddUnsubThenPublishOnce(Ctx c)
    {
        var log = c.Log;
        var sub = c.Hub.Subscribe(async e => { await Task.Yield(); log.Add($"once:{e.Id}"); });
        await PublishAsync(c, 10, "a");
        sub.Dispose();
        return c;
    }

    // Scenarios

    [Scenario("PublishAsync delivers to all async subscribers in order")]
    [Fact]
    public async Task Basic_Subscribe_Publish()
    {
        await Given("a default async observer", Build_Default)
            .When("subscribing two async 'all' handlers", c =>
            {
                var log = c.Log;
                c.Hub.Subscribe(async e => { await Task.Delay(1); log.Add($"h1:{e.Id}"); });
                c.Hub.Subscribe(async e => { await Task.Delay(1); log.Add($"h2:{e.Id}"); });
                return ValueTask.FromResult(c);
            })
            .When("publishing event #7", c => PublishAsync(c, 7, "x"))
            .Then("both handlers ran in order", c => string.Join(',', c.Log) == "h1:7,h2:7")
            .AssertPassed();
    }

    [Scenario("Predicate filters (async) route events to matching subscribers only")]
    [Fact]
    public async Task Predicate_Filtering()
    {
        await Given("observer with all + filtered async subscribers", Build_Default)
            .When("adding an 'all' handler and two filtered handlers", AddAllAndFiltered)
            .When("publishing #1 and #2", c => PublishTwo(c, 1, "one", 2, "two"))
            .Then("log shows all + matching filtered entries", c =>
            {
                var s = string.Join('|', c.Log);
                return s.Contains("all:1:one") && s.Contains("odd:1") && s.Contains("all:2:two") && s.Contains("even:2");
            })
            .AssertPassed();
    }

    [Scenario("Unsubscribing stops delivery (async)")]
    [Fact]
    public async Task Unsubscribe_Works()
    {
        await Given("observer and a tracked subscription", Build_Default)
            .When("adding an unsubscribable handler", AddUnsubThenPublishOnce)
            .When("publishing again after dispose", c => PublishAsync(c, 11, "b"))
            .Then("only the first publish is logged", c => string.Join(',', c.Log) == "once:10")
            .AssertPassed();
    }

    [Scenario("SwallowErrors: exceptions don't propagate; others continue (async)")] 
    [Fact]
    public async Task SwallowErrors_Allows_Continuation()
    {
        var sink = new List<(Exception, Evt)>();
        await Given("async observer with swallow policy and sink", () => Build_With_Sink_Swallow(sink))
            .When("adding throwing + normal handlers", c =>
            {
                c.Hub.Subscribe(static _ => throw new InvalidOperationException("boom"));
                c.Hub.Subscribe(async e => { await Task.Yield(); c.Log.Add($"ok:{e.Id}"); });
                return ValueTask.FromResult(c);
            })
            .When("publishing event", c => PublishAsync(c, 1, "x"))
            .Then("no exception escaped and normal ran", c => c.Log.SequenceEqual(["ok:1"]))
            .And("sink captured the exception", _ => sink is [{ Item1: InvalidOperationException } _])
            .AssertPassed();
    }

    [Scenario("ThrowFirstError: first exception is rethrown; later handlers don't run (async)")]
    [Fact]
    public async Task ThrowFirst_Stops_And_Propagates()
    {
        var sink = new List<(Exception, Evt)>();
        await Given("async observer with throw-first policy", () => Build_With_Sink_ThrowFirst(sink))
            .When("adding throwing then normal", c =>
            {
                c.Hub.Subscribe(static _ => throw new ApplicationException("x"));
                c.Hub.Subscribe(async e => { await Task.Yield(); c.Log.Add($"not:{e.Id}"); });
                return ValueTask.FromResult(c);
            })
            .When("publishing and catching", c => PublishCatch<ApplicationException>(c, new Evt(5, "x"), "threw"))
            .Then("later handler did not run", c => !c.Log.Contains("not:5"))
            .And("sink recorded the exception", _ => sink is [{ Item1: ApplicationException } _])
            .AssertPassed();
    }

    [Scenario("ThrowAggregate: collect multiple exceptions and throw one AggregateException (async)")]
    [Fact]
    public async Task ThrowAggregate_Collects_All()
    {
        var sink = new List<(Exception, Evt)>();
        await Given("async observer with aggregate policy", () => Build_With_Sink_Aggregate(sink))
            .When("adding two throwing and one normal", c =>
            {
                c.Hub.Subscribe(static _ => throw new InvalidOperationException("a"));
                c.Hub.Subscribe(async e => { await Task.Yield(); c.Log.Add($"ok:{e.Id}"); });
                c.Hub.Subscribe(static _ => throw new ArgumentException("b"));
                return ValueTask.FromResult(c);
            })
            .When("publishing and catching aggregate", c => PublishCatch<AggregateException>(c, new Evt(9, "n"), marker: "agg:2"))
            .Then("normal handler ran despite throws", c => c.Log.Contains("ok:9"))
            .And("aggregate captured both exceptions", c => c.Log.Contains("agg:2"))
            .And("sink saw two exceptions", _ => sink.Count == 2)
            .AssertPassed();
    }

    [Scenario("Sync adapter overloads interop with AsyncObserver")]
    [Fact]
    public async Task Sync_Adapters_Work()
    {
        await Given("async observer", Build_Default)
            .When("adding sync-style subscriptions via adapters", c =>
            {
                c.Hub.Subscribe(static (in e) => e.Id > 0, (in e) => { c.Log.Add($"sync:{e.Id}"); });
                c.Hub.Subscribe((in e) => { c.Log.Add($"all:{e.Id}"); });
                return ValueTask.FromResult(c);
            })
            .When("publishing event #3", c => PublishAsync(c, 3, "x"))
            .Then("both sync adapters ran", c => c.Log.SequenceEqual(["sync:3", "all:3"]))
            .AssertPassed();
    }
}
