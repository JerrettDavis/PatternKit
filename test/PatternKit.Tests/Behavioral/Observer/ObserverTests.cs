using PatternKit.Behavioral.Observer;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Observer;

[Feature("Observer<TEvent> (typed, fluent, thread-safe event hub)")]
public sealed class ObserverTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private readonly record struct Evt(int Id, string Name);

    private sealed record Ctx(Observer<Evt> Hub, List<string> Log)
    {
        public Ctx() : this(Observer<Evt>.Create().Build(), []) { }
    }

    private static Ctx Build_Default() => new();

    private static Ctx Build_With_Sink_Aggregate(List<(Exception ex, Evt evt)> sink)
        => new(
            Observer<Evt>.Create()
                .OnError((ex, in e) => sink.Add((ex, e)))
                .ThrowAggregate()
                .Build(),
            []);

    private static Ctx Build_With_Sink_Swallow(List<(Exception ex, Evt evt)> sink)
        => new(
            Observer<Evt>.Create()
                .OnError((ex, in e) => sink.Add((ex, e)))
                .SwallowErrors()
                .Build(),
            []);

    private static Ctx Build_With_Sink_ThrowFirst(List<(Exception ex, Evt evt)> sink)
        => new(
            Observer<Evt>.Create()
                .OnError((ex, in e) => sink.Add((ex, e)))
                .ThrowFirstError()
                .Build(),
            []);

    private static Ctx Sub_All(Ctx c)
    {
        var log = c.Log;
        c.Hub.Subscribe((in e) => log.Add($"all:{e.Id}:{e.Name}"));
        return c;
    }

    private static Ctx Sub_Filtered(Ctx c)
    {
        var log = c.Log;
        c.Hub.Subscribe(static (in e) => e.Id % 2 == 0, (in e) => log.Add($"even:{e.Id}"));
        c.Hub.Subscribe(static (in e) => e.Id % 2 == 1, (in e) => log.Add($"odd:{e.Id}"));
        return c;
    }

    private static Ctx Publish(Ctx c, int id, string name)
    {
        var ev = new Evt(id, name);
        c.Hub.Publish(in ev);
        return c;
    }

    [Scenario("Publish delivers to all subscribers in registration order")]
    [Fact]
    public async Task Basic_Subscribe_Publish()
    {
        await Given("a default observer and two subscribers", Build_Default)
            .When("subscribing two 'all' handlers", c =>
            {
                var log = c.Log;
                c.Hub.Subscribe((in e) => log.Add($"h1:{e.Id}"));
                c.Hub.Subscribe((in e) => log.Add($"h2:{e.Id}"));
                return c;
            })
            .When("publishing event #7", c => Publish(c, 7, "x"))
            .Then("both handlers ran in order", c => string.Join(',', c.Log) == "h1:7,h2:7")
            .AssertPassed();
    }

    [Scenario("Predicate filters route events to matching subscribers only")]
    [Fact]
    public async Task Predicate_Filtering()
    {
        await Given("observer with all + filtered subscribers", Build_Default)
            .When("adding an 'all' handler and two filtered handlers", c => { Sub_All(c); Sub_Filtered(c); return c; })
            .When("publishing #1 and #2", c => { Publish(c, 1, "one"); Publish(c, 2, "two"); return c; })
            .Then("log shows all + matching filtered entries", c =>
            {
                var s = string.Join('|', c.Log);
                return s.Contains("all:1:one") && s.Contains("odd:1") && s.Contains("all:2:two") && s.Contains("even:2");
            })
            .AssertPassed();
    }

    [Scenario("Unsubscribing stops delivery")]
    [Fact]
    public async Task Unsubscribe_Works()
    {
        await Given("observer and a tracked subscription", Build_Default)
            .When("adding an unsubscribable handler", c =>
            {
                var log = c.Log;
                var sub = c.Hub.Subscribe((in e) => log.Add($"once:{e.Id}"));
                // publish once
                Publish(c, 10, "a");
                sub.Dispose();
                return c;
            })
            .When("publishing again after dispose", c => Publish(c, 11, "b"))
            .Then("only the first publish is logged", c => string.Join(',', c.Log) == "once:10")
            .AssertPassed();
    }

    [Scenario("SwallowErrors: exceptions don't propagate; others continue")] 
    [Fact]
    public async Task SwallowErrors_Allows_Continuation()
    {
        var sink = new List<(Exception, Evt)>();
        await Given("observer with swallow policy and sink", () => Build_With_Sink_Swallow(sink))
            .When("adding throwing + normal handlers", c =>
            {
                c.Hub.Subscribe(static (in _) => throw new InvalidOperationException("boom"));
                c.Hub.Subscribe((in e) => c.Log.Add($"ok:{e.Id}"));
                return c;
            })
            .When("publishing event", c => Publish(c, 1, "x"))
            .Then("no exception escaped and normal ran", c => c.Log.SequenceEqual(["ok:1"]))
            .And("sink captured the exception", _ => sink.Count == 1 && sink[0].Item1 is InvalidOperationException)
            .AssertPassed();
    }

    [Scenario("ThrowFirstError: first exception is rethrown; later handlers don't run")]
    [Fact]
    public async Task ThrowFirst_Stops_And_Propagates()
    {
        var sink = new List<(Exception, Evt)>();
        await Given("observer with throw-first policy", () => Build_With_Sink_ThrowFirst(sink))
            .When("adding throwing then normal", c =>
            {
                c.Hub.Subscribe(static (in _) => throw new ApplicationException("x"));
                c.Hub.Subscribe((in e) => c.Log.Add($"not:{e.Id}"));
                return c;
            })
            .When("publishing and catching", c =>
            {
                var ev = new Evt(5, "x");
                try { c.Hub.Publish(in ev); }
                catch (ApplicationException) { c.Log.Add("threw"); }
                return c;
            })
            .Then("later handler did not run", c => !c.Log.Contains("not:5"))
            .And("sink recorded the exception", _ => sink.Count == 1 && sink[0].Item1 is ApplicationException)
            .AssertPassed();
    }

    [Scenario("ThrowAggregate: collect multiple exceptions and throw one AggregateException")]
    [Fact]
    public async Task ThrowAggregate_Collects_All()
    {
        var sink = new List<(Exception, Evt)>();
        await Given("observer with aggregate policy", () => Build_With_Sink_Aggregate(sink))
            .When("adding two throwing and one normal", c =>
            {
                c.Hub.Subscribe(static (in _) => throw new InvalidOperationException("a"));
                c.Hub.Subscribe((in e) => c.Log.Add($"ok:{e.Id}"));
                c.Hub.Subscribe(static (in _) => throw new ArgumentException("b"));
                return c;
            })
            .When("publishing and catching aggregate", c =>
            {
                var ev = new Evt(9, "n");
                try { c.Hub.Publish(in ev); }
                catch (AggregateException ae)
                {
                    c.Log.Add($"agg:{ae.InnerExceptions.Count}");
                }
                return c;
            })
            .Then("normal handler ran despite throws", c => c.Log.Contains("ok:9"))
            .And("aggregate captured both exceptions", c => c.Log.Contains("agg:2"))
            .And("sink saw two exceptions", _ => sink.Count == 2)
            .AssertPassed();
    }
}
