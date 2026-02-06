using PatternKit.Examples.Generators.State;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.State;

[Feature("State Generator Example")]
public sealed class StateGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Turnstile transitions through states correctly")]
    [Fact]
    public Task TurnstileTransitions() =>
        Given("running the turnstile demo", () => true)
        .When("the demo executes", _ => StateGeneratorDemo.Run())
        .Then("state transitions are logged correctly", log =>
        {
            Assert.Contains(log, l => l.Contains("Initial state: Locked"));
            Assert.Contains(log, l => l.Contains("State after coin: Unlocked"));
            Assert.Contains(log, l => l.Contains("State after push: Locked"));
        })
        .AssertPassed();

    [Scenario("CanFire checks are correct")]
    [Fact]
    public Task CanFireChecks() =>
        Given("running the turnstile demo", () => true)
        .When("the demo executes", _ => StateGeneratorDemo.Run())
        .Then("CanFire results are correct", log =>
        {
            Assert.Contains(log, l => l.Contains("Can push when locked? False"));
            Assert.Contains(log, l => l.Contains("Can insert coin when locked? True"));
        })
        .AssertPassed();

    [Scenario("Entry and exit hooks fire during transitions")]
    [Fact]
    public Task EntryExitHooksFire() =>
        Given("running the turnstile demo", () => true)
        .When("the demo executes", _ => StateGeneratorDemo.Run())
        .Then("entry and exit hooks are in the log", log =>
        {
            Assert.Contains(log, l => l.Contains("[Entry] Turnstile is now unlocked"));
            Assert.Contains(log, l => l.Contains("[Exit] Leaving locked state"));
        })
        .AssertPassed();

    [Scenario("Demo runs without errors")]
    [Fact]
    public Task DemoRunsSuccessfully() =>
        Given("the state generator demo", () => true)
        .When("the demo is executed", _ => StateGeneratorDemo.Run())
        .Then("it produces output without throwing", log =>
        {
            Assert.NotEmpty(log);
        })
        .AssertPassed();
}
