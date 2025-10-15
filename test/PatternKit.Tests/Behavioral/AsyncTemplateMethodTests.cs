using PatternKit.Behavioral.Template;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - AsyncTemplateMethod<TContext,TResult> (async skeleton with hooks)")]
public sealed class AsyncTemplateMethodTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed class SampleAsyncTemplate : AsyncTemplateMethod<int, string>
    {
        public int BeforeCount;
        public int AfterCount;
        public int Concurrent;
        public int MaxConcurrent;
        private readonly int _delayMs;
        private readonly bool _sync;

        public SampleAsyncTemplate(int delayMs = 10, bool sync = false)
        {
            _delayMs = delayMs;
            _sync = sync;
        }

        protected override bool Synchronized => _sync;

        protected override async ValueTask OnBeforeAsync(int context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref BeforeCount);
            var c = Interlocked.Increment(ref Concurrent);
            Volatile.Write(ref MaxConcurrent, Math.Max(Volatile.Read(ref MaxConcurrent), c));
            await Task.Yield();
        }

        protected override async ValueTask<string> StepAsync(int context, CancellationToken cancellationToken)
        {
            await Task.Delay(_delayMs, cancellationToken);
            return $"R-{context}";
        }

        protected override async ValueTask OnAfterAsync(int context, string result, CancellationToken cancellationToken)
        {
            Interlocked.Decrement(ref Concurrent);
            Interlocked.Increment(ref AfterCount);
            await Task.Yield();
        }
    }

    [Scenario("ExecuteAsync runs hooks and returns result")]
    [Fact]
    public Task ExecuteAsync_RunsHooks_And_Returns()
        => Given("a sample async template", () => new SampleAsyncTemplate())
           .When("ExecuteAsync(7)", t => (tpl: t, res: t.ExecuteAsync(7).GetAwaiter().GetResult()))
           .Then("result is R-7", r => r.res == "R-7")
           .And("before called once", r => r.tpl.BeforeCount == 1)
           .And("after called once", r => r.tpl.AfterCount == 1)
           .AssertPassed();

    [Scenario("Not synchronized allows concurrency")]
    [Fact]
    public Task Allows_Concurrency_When_Not_Synchronized()
        => Given("a non-synchronized template with delay", () => new SampleAsyncTemplate(delayMs: 25, sync: false))
           .When("ExecuteAsync on 1..4 in parallel", t =>
           {
               var tasks = new[] { t.ExecuteAsync(1), t.ExecuteAsync(2), t.ExecuteAsync(3), t.ExecuteAsync(4) };
               var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
               return (tpl: t, results);
           })
           .Then("contains R-1", r => r.results.Contains("R-1"))
           .And("max concurrency > 1", r => r.tpl.MaxConcurrent > 1)
           .AssertPassed();

    [Scenario("Synchronized serializes ExecuteAsync calls")]
    [Fact]
    public Task Serializes_When_Synchronized()
        => Given("a synchronized template with delay", () => new SampleAsyncTemplate(delayMs: 20, sync: true))
           .When("ExecuteAsync on 1..4 in parallel", t =>
           {
               var tasks = new[] { t.ExecuteAsync(1), t.ExecuteAsync(2), t.ExecuteAsync(3), t.ExecuteAsync(4) };
               var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
               return (tpl: t, results);
           })
           .Then("contains R-1", r => r.results.Contains("R-1"))
           .And("max concurrency == 1", r => r.tpl.MaxConcurrent == 1)
           .AssertPassed();

    [Scenario("Cancellation is observed")]
    [Fact]
    public Task Cancellation_Observed()
        => Given("a template with long delay and a CTS", () => (tpl: new SampleAsyncTemplate(delayMs: 100), cts: new CancellationTokenSource(10)))
           .When("ExecuteAsync with cancellation", ctx =>
           {
               try { ctx.tpl.ExecuteAsync(1, ctx.cts.Token).GetAwaiter().GetResult(); return false; }
               catch (OperationCanceledException) { return true; }
           })
           .Then("throws OperationCanceledException", threw => threw)
           .AssertPassed();
}