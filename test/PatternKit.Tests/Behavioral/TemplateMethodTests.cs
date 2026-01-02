using PatternKit.Behavioral.Template;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - TemplateMethod<TContext,TResult> (skeleton with hooks)")]
public sealed class TemplateMethodTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed class TestTemplate : TemplateMethod<string, string>
    {
        public bool BeforeCalled { get; private set; }
        public bool AfterCalled { get; private set; }

        protected override void OnBefore(string context) => BeforeCalled = true;
        protected override string Step(string context) => context.ToUpperInvariant();
        protected override void OnAfter(string context, string result) => AfterCalled = true;
    }

    [Scenario("Executes algorithm and before/after hooks in order")]
    [Fact]
    public Task Executes_Algorithm_And_Hooks()
        => Given("a template instance", () => new TestTemplate())
           .When("executed with 'test'", t => (template: t, result: t.Execute("test")))
           .Then("returns transformed result", r => r.result == "TEST")
           .And("before hook called", r => r.template.BeforeCalled)
           .And("after hook called", r => r.template.AfterCalled)
           .AssertPassed();

    [Scenario("Concurrent executions are safe")]
    [Fact]
    public Task Is_Thread_Safe()
        => Given("a template and 10 inputs", () => (tpl: new TestTemplate(), inputs: Enumerable.Range(0, 10).Select(i => $"thread-{i}").ToArray()))
           .When("executed in parallel", ctx =>
           {
               var results = new string[ctx.inputs.Length];
               Parallel.For(0, ctx.inputs.Length, i => results[i] = ctx.tpl.Execute(ctx.inputs[i]));
               return results;
           })
           .Then("all results upper-cased", results => results.All(s => s.StartsWith("THREAD-")))
           .AssertPassed();
}

#region Additional TemplateMethod Tests

public sealed class TemplateMethodBuilderTests
{
    private sealed class SynchronizedTemplate : TemplateMethod<int, int>
    {
        private int _concurrentCount;
        private int _maxConcurrent;

        protected override bool Synchronized => true;

        protected override int Step(int context)
        {
            var c = Interlocked.Increment(ref _concurrentCount);
            while (true)
            {
                var snap = Volatile.Read(ref _maxConcurrent);
                var next = Math.Max(snap, c);
                if (Interlocked.CompareExchange(ref _maxConcurrent, next, snap) == snap) break;
            }
            Thread.Sleep(10);
            Interlocked.Decrement(ref _concurrentCount);
            return context * 2;
        }

        public int MaxConcurrent => _maxConcurrent;
    }

    private sealed class MinimalTemplate : TemplateMethod<string, int>
    {
        protected override int Step(string context) => context.Length;
    }

    private sealed class HooksTemplate : TemplateMethod<int, string>
    {
        public List<string> Log { get; } = new();

        protected override void OnBefore(int context) => Log.Add($"before:{context}");
        protected override string Step(int context) => $"result:{context}";
        protected override void OnAfter(int context, string result) => Log.Add($"after:{context}:{result}");
    }

    [Fact]
    public void Synchronized_Enforces_Mutual_Exclusion()
    {
        var template = new SynchronizedTemplate();

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => template.Execute(1))).ToArray();
        Task.WaitAll(tasks);

        Assert.Equal(1, template.MaxConcurrent);
    }

    [Fact]
    public void Synchronized_Returns_Correct_Results()
    {
        var template = new SynchronizedTemplate();

        var result = template.Execute(21);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Minimal_Template_Works()
    {
        var template = new MinimalTemplate();

        var result = template.Execute("hello");

        Assert.Equal(5, result);
    }

    [Fact]
    public void Hooks_Called_In_Order()
    {
        var template = new HooksTemplate();

        var result = template.Execute(42);

        Assert.Equal("result:42", result);
        Assert.Equal(2, template.Log.Count);
        Assert.Equal("before:42", template.Log[0]);
        Assert.Equal("after:42:result:42", template.Log[1]);
    }

    [Fact]
    public void Multiple_Executions_Independent()
    {
        var template = new HooksTemplate();

        template.Execute(1);
        template.Execute(2);
        template.Execute(3);

        Assert.Equal(6, template.Log.Count);
    }
}

#endregion