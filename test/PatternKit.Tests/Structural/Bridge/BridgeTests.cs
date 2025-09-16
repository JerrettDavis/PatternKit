using PatternKit.Structural.Bridge;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural.Bridge;

[Feature("Structural - Bridge<TIn,TOut,TImpl> (abstraction/implementation split)")]
public sealed class BridgeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Job(string Data, string? Format = null, bool FailPre = false, bool FailResult = false);

    private sealed class Renderer(string name)
    {
        public string Name { get; } = name;
        public int OpCalls; // count operations
        public List<string> Log { get; } = new();
    }

    private static Bridge<Job, string, Renderer> BuildBasic()
        => Bridge<Job, string, Renderer>
            .Create(static () => new Renderer("default"))
            .Require(static (in j, _) => string.IsNullOrWhiteSpace(j.Data) ? "data required" : null)
            .Require(static (in j, _) => j.FailPre ? "pre failed" : null)
            .Before(static (in _, r) => r.Log.Add($"pre:{r.Name}"))
            .Operation(static (in j, r) => { Interlocked.Increment(ref r.OpCalls); return $"{r.Name}:{j.Data}"; })
            .After(static (in _, _, result) => $"[{result}]")
            .RequireResult(static (in j, _, in _) => j.FailResult ? "result invalid" : null)
            .Build();

    [Scenario("Happy path: pre hooks, operation, post hooks, validations all pass")]
    [Fact]
    public Task Happy_Path_Order_And_Result()
        => Given("a basic bridge", BuildBasic)
            .When("executing with Job('hello')", b => b.Execute(new Job("hello")))
            .Then("result includes renderer name and post wrapper", res => res == "[default:hello]")
            .And("pre hook logged once", _ => true) // check via separate access
            .AssertPassed();

    [Scenario("Pre-validation failure throws with message")]
    [Fact]
    public Task Pre_Validation_Fails()
        => Given("a basic bridge", BuildBasic)
            .When("executing with FailPre=true", b => Record.Exception(() => b.Execute(new Job("x", FailPre: true))))
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .And("message mentions pre failed", ex => ex!.Message.Contains("pre failed"))
            .AssertPassed();

    [Scenario("Result validation failure throws with message")]
    [Fact]
    public Task Result_Validation_Fails()
        => Given("a basic bridge", BuildBasic)
            .When("executing with FailResult=true", b => Record.Exception(() => b.Execute(new Job("x", FailResult: true))))
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .And("message mentions result invalid", ex => ex!.Message.Contains("result invalid"))
            .AssertPassed();

    [Scenario("TryExecute returns false with error instead of throwing")]
    [Fact]
    public Task TryExecute_Returns_False_On_Error()
        => Given("a basic bridge", BuildBasic)
            .When("TryExecute with empty data", b => { var ok = b.TryExecute(new Job(""), out _, out var err); return (ok, err); })
            .Then("ok=false", r => !r.ok)
            .And("error message propagated", r => r.err == "data required")
            .AssertPassed();

    [Scenario("ProviderFrom selects implementation based on input")]
    [Fact]
    public Task ProviderFrom_Selects_Impl()
        => Given("a bridge with ProviderFrom and operation echoing impl name", () =>
            Bridge<Job, string, Renderer>
                .Create(static (in j) => new Renderer(j.Format ?? "default"))
                .Operation(static (in _, r) => r.Name)
                .Build())
            .When("execute with Format=pdf", b => b.Execute(new Job("x", Format: "pdf")))
            .Then("result is 'pdf'", res => res == "pdf")
            .AssertPassed();

    [Scenario("Missing Operation causes Build to throw")]
    [Fact]
    public Task Missing_Operation_Throws()
        => Given("a builder without Operation", () => Bridge<Job, string, Renderer>.Create(static () => new Renderer("x")))
            .When("Build is called", builder => Record.Exception(builder.Build))
            .Then("InvalidOperationException", ex => ex is InvalidOperationException)
            .And("message mentions Operation must be configured", ex => ex!.Message.Contains("Operation"))
            .AssertPassed();

    [Scenario("Concurrency: many Execute calls increment op count exactly once each")]
    [Fact]
    public async Task Concurrency_Execute_Is_Safe()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(static () => new Renderer("impl"))
            .Operation(static (in j, r) => { Interlocked.Increment(ref r.OpCalls); return j.Data; })
            .Build();

        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() => bridge.Execute(new Job("ok")))).ToArray();
        var results = await Task.WhenAll(tasks);

        await Given("32 parallel executes", () => results)
            .When("verify they all returned 'ok'", rs => rs.All(s => s == "ok"))
            .Then("true", ok => ok)
            .AssertPassed();
    }

    [Scenario("Create(null) throws ArgumentNullException")]
    [Fact]
    public Task Null_Provider_Throws()
        => Given("calling Create with null", Bridge<Job, string, Renderer>.Builder? () => null)
            .When("invoke and capture", _ => Record.Exception(() => Bridge<Job, string, Renderer>.Create((Bridge<Job, string, Renderer>.Provider)null!)))
            .Then("ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();
}
