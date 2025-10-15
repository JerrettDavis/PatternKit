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