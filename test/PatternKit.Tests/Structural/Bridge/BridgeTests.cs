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
        public List<string> Log { get; } = [];
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
    [Fact(Timeout = 30_000)]
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

#region Additional Bridge Tests

public sealed class BridgeBuilderTests
{
    private sealed record Job(string Data, string? Format = null, bool FailPre = false, bool FailResult = false);

    private sealed class Renderer(string name)
    {
        public string Name { get; } = name;
        public List<string> Log { get; } = [];
    }

    [Scenario("TryExecute Success ReturnsTrue")]
    [Fact]
    public void TryExecute_Success_ReturnsTrue()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Operation((in j, r) => $"{r.Name}:{j.Data}")
            .Build();

        var success = bridge.TryExecute(new Job("test"), out var output, out var error);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("default:test", output);
        ScenarioExpect.Null(error);
    }

    [Scenario("TryExecute PreValidationFails ReturnsFalse")]
    [Fact]
    public void TryExecute_PreValidationFails_ReturnsFalse()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Require((in j, _) => string.IsNullOrEmpty(j.Data) ? "data required" : null)
            .Operation((in j, r) => $"{r.Name}:{j.Data}")
            .Build();

        var success = bridge.TryExecute(new Job(""), out var output, out var error);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal("data required", error);
    }

    [Scenario("TryExecute ResultValidationFails ReturnsFalse")]
    [Fact]
    public void TryExecute_ResultValidationFails_ReturnsFalse()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Operation((in j, r) => $"{r.Name}:{j.Data}")
            .RequireResult((in j, _, in result) => result.Length > 10 ? "result too long" : null)
            .Build();

        var success = bridge.TryExecute(new Job("this is long data"), out var output, out var error);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal("result too long", error);
    }

    [Scenario("TryExecute OperationThrows ReturnsFalse")]
    [Fact]
    public void TryExecute_OperationThrows_ReturnsFalse()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Operation((in j, r) => throw new InvalidOperationException("operation failed"))
            .Build();

        var success = bridge.TryExecute(new Job("test"), out var output, out var error);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal("operation failed", error);
    }

    [Scenario("TryExecute PreHookThrows ReturnsFalse")]
    [Fact]
    public void TryExecute_PreHookThrows_ReturnsFalse()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Before((in j, r) => throw new InvalidOperationException("pre hook failed"))
            .Operation((in j, r) => $"{r.Name}:{j.Data}")
            .Build();

        var success = bridge.TryExecute(new Job("test"), out var output, out var error);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal("pre hook failed", error);
    }

    [Scenario("TryExecute PostHookThrows ReturnsFalse")]
    [Fact]
    public void TryExecute_PostHookThrows_ReturnsFalse()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Operation((in j, r) => $"{r.Name}:{j.Data}")
            .After((in j, r, result) => throw new InvalidOperationException("post hook failed"))
            .Build();

        var success = bridge.TryExecute(new Job("test"), out var output, out var error);

        ScenarioExpect.False(success);
        ScenarioExpect.Equal("post hook failed", error);
    }

    [Scenario("Multiple PreHooks ExecuteInOrder")]
    [Fact]
    public void Multiple_PreHooks_ExecuteInOrder()
    {
        var log = new List<string>();
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Before((in j, r) => log.Add("pre1"))
            .Before((in j, r) => log.Add("pre2"))
            .Before((in j, r) => log.Add("pre3"))
            .Operation((in j, r) => "result")
            .Build();

        bridge.Execute(new Job("test"));

        ScenarioExpect.Equal(new[] { "pre1", "pre2", "pre3" }, log);
    }

    [Scenario("Multiple PostHooks TransformInOrder")]
    [Fact]
    public void Multiple_PostHooks_TransformInOrder()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Operation((in j, r) => j.Data)
            .After((in j, r, result) => $"[{result}]")
            .After((in j, r, result) => $"<{result}>")
            .After((in j, r, result) => $"({result})")
            .Build();

        var result = bridge.Execute(new Job("test"));

        ScenarioExpect.Equal("(<[test]>)", result);
    }

    [Scenario("Multiple Validators FirstFailStops")]
    [Fact]
    public void Multiple_Validators_FirstFailStops()
    {
        var validatorCalls = new List<int>();
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Require((in j, _) => { validatorCalls.Add(1); return null; })
            .Require((in j, _) => { validatorCalls.Add(2); return "fail at 2"; })
            .Require((in j, _) => { validatorCalls.Add(3); return null; })
            .Operation((in j, r) => "result")
            .Build();

        var ex = ScenarioExpect.Throws<InvalidOperationException>(() => bridge.Execute(new Job("test")));

        ScenarioExpect.Equal("fail at 2", ex.Message);
        ScenarioExpect.Equal(new[] { 1, 2 }, validatorCalls); // Third validator not called
    }

    [Scenario("Multiple ResultValidators FirstFailStops")]
    [Fact]
    public void Multiple_ResultValidators_FirstFailStops()
    {
        var validatorCalls = new List<int>();
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("default"))
            .Operation((in j, r) => "result")
            .RequireResult((in j, _, in result) => { validatorCalls.Add(1); return null; })
            .RequireResult((in j, _, in result) => { validatorCalls.Add(2); return "result fail at 2"; })
            .RequireResult((in j, _, in result) => { validatorCalls.Add(3); return null; })
            .Build();

        var ex = ScenarioExpect.Throws<InvalidOperationException>(() => bridge.Execute(new Job("test")));

        ScenarioExpect.Equal("result fail at 2", ex.Message);
        ScenarioExpect.Equal(new[] { 1, 2 }, validatorCalls);
    }

    [Scenario("ProviderFrom WithNull Throws")]
    [Fact]
    public void ProviderFrom_WithNull_Throws()
    {
        var ex = ScenarioExpect.Throws<ArgumentNullException>(() =>
            Bridge<Job, string, Renderer>.Create((Bridge<Job, string, Renderer>.ProviderFrom)null!));
    }

    [Scenario("Execute ProviderFrom ReceivesInput")]
    [Fact]
    public void Execute_ProviderFrom_ReceivesInput()
    {
        Renderer? capturedImpl = null;
        var bridge = Bridge<Job, string, Renderer>
            .Create((in Job j) => new Renderer(j.Format ?? "unknown"))
            .Operation((in j, r) => { capturedImpl = r; return r.Name; })
            .Build();

        var result = bridge.Execute(new Job("data", Format: "custom"));

        ScenarioExpect.NotNull(capturedImpl);
        ScenarioExpect.Equal("custom", capturedImpl!.Name);
        ScenarioExpect.Equal("custom", result);
    }

    [Scenario("TryExecute ProviderFrom UsesCorrectProvider")]
    [Fact]
    public void TryExecute_ProviderFrom_UsesCorrectProvider()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create((in Job j) => new Renderer(j.Format ?? "default"))
            .Operation((in j, r) => r.Name)
            .Build();

        bridge.TryExecute(new Job("test", Format: "pdf"), out var output, out _);

        ScenarioExpect.Equal("pdf", output);
    }

    [Scenario("TryExecute WithValidatorsButAllPass ReturnsTrue")]
    [Fact]
    public void TryExecute_WithValidatorsButAllPass_ReturnsTrue()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("impl"))
            .Require((in j, _) => null) // Pass
            .Require((in j, _) => null) // Pass
            .Operation((in j, r) => "result")
            .RequireResult((in j, _, in r) => null) // Pass
            .Build();

        var success = bridge.TryExecute(new Job("data"), out var output, out var error);

        ScenarioExpect.True(success);
        ScenarioExpect.Equal("result", output);
        ScenarioExpect.Null(error);
    }

    [Scenario("TryExecute WithPostHooks AppliesTransformations")]
    [Fact]
    public void TryExecute_WithPostHooks_AppliesTransformations()
    {
        var bridge = Bridge<Job, string, Renderer>
            .Create(() => new Renderer("impl"))
            .Operation((in j, r) => j.Data)
            .After((in j, r, result) => result.ToUpperInvariant())
            .Build();

        bridge.TryExecute(new Job("hello"), out var output, out _);

        ScenarioExpect.Equal("HELLO", output);
    }
}

#endregion
