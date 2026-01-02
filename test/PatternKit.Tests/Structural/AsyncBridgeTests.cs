using PatternKit.Structural.Bridge;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural;

[Feature("AsyncBridge<TIn,TOut,TImpl> (async bridge pattern)")]
public sealed class AsyncBridgeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed class MockRenderer
    {
        public List<string> Log { get; } = new();
        public void BeforeRender(string s) => Log.Add($"before:{s}");
        public void AfterRender(string s) => Log.Add($"after:{s}");
        public string Render(string s) => $"<{s}>";
    }

    private sealed record Ctx(
        AsyncBridge<string, string, MockRenderer> Bridge,
        MockRenderer Renderer,
        string? Result = null,
        (bool success, string? result, string? error)? TryResult = null
    );

    private static Ctx Build_Basic()
    {
        var renderer = new MockRenderer();
        var bridge = AsyncBridge<string, string, MockRenderer>
            .Create(() => renderer)
            .Operation((input, impl, _) => new ValueTask<string>(impl.Render(input)))
            .Build();

        return new Ctx(bridge, renderer);
    }

    private static Ctx Build_WithHooks()
    {
        var renderer = new MockRenderer();
        var bridge = AsyncBridge<string, string, MockRenderer>
            .Create(() => renderer)
            .Before((input, impl) => impl.BeforeRender(input))
            .Operation((input, impl, _) => new ValueTask<string>(impl.Render(input)))
            .After((input, impl, result) => result.ToUpperInvariant())
            .Build();

        return new Ctx(bridge, renderer);
    }

    private static Ctx Build_WithValidation()
    {
        var renderer = new MockRenderer();
        var bridge = AsyncBridge<string, string, MockRenderer>
            .Create(() => renderer)
            .Require((input, _) => string.IsNullOrEmpty(input) ? "Input cannot be empty" : null)
            .Operation((input, impl, _) => new ValueTask<string>(impl.Render(input)))
            .RequireResult((_, _, result) => result.Length < 3 ? "Result too short" : null)
            .Build();

        return new Ctx(bridge, renderer);
    }

    private static async Task<Ctx> ExecAsync(Ctx c, string input)
    {
        var result = await c.Bridge.ExecuteAsync(input);
        return c with { Result = result };
    }

    private static async Task<Ctx> TryExecAsync(Ctx c, string input)
    {
        var result = await c.Bridge.TryExecuteAsync(input);
        return c with { TryResult = result };
    }

    [Scenario("Basic async bridge execution")]
    [Fact]
    public async Task BasicExecution()
    {
        await Given("an async bridge with a render operation", Build_Basic)
            .When("executing with 'hello'", c => ExecAsync(c, "hello"))
            .Then("returns '<hello>'", c => c.Result == "<hello>")
            .AssertPassed();
    }

    [Scenario("Before and after hooks execute")]
    [Fact]
    public async Task HooksExecute()
    {
        await Given("an async bridge with hooks", Build_WithHooks)
            .When("executing with 'test'", c => ExecAsync(c, "test"))
            .Then("result is uppercase", c => c.Result == "<TEST>")
            .And("before hook ran", c => c.Renderer.Log.Contains("before:test"))
            .AssertPassed();
    }

    [Scenario("Validation prevents execution")]
    [Fact]
    public async Task ValidationPreventsExecution()
    {
        await Given("an async bridge with validation", Build_WithValidation)
            .When("try-executing with empty string", c => TryExecAsync(c, ""))
            .Then("returns failure", c => !c.TryResult!.Value.success)
            .And("error mentions 'empty'", c => c.TryResult!.Value.error!.Contains("empty"))
            .AssertPassed();
    }

    [Scenario("Result validation works")]
    [Fact]
    public async Task ResultValidation()
    {
        await Given("an async bridge with result validation", Build_WithValidation)
            .When("try-executing with 'a' (result <a> = 3 chars, passes)", c => TryExecAsync(c, "a"))
            .Then("returns success", c => c.TryResult!.Value.success)
            .AssertPassed();
    }
}
