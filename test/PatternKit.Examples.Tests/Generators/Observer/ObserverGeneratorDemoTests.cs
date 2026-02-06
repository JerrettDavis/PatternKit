using PatternKit.Examples.Generators.Observer;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.Observer;

[Feature("Observer Generator Example")]
public sealed class ObserverGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Stock price feed publishes to all subscribers")]
    [Fact]
    public Task PublishesToAllSubscribers() =>
        Given("running the stock price demo", () => true)
        .When("the demo executes", _ => ObserverGeneratorDemo.Run())
        .Then("all subscribers receive notifications", log =>
        {
            Assert.Contains(log, l => l.Contains("Dashboard: ACME"));
            Assert.Contains(log, l => l.Contains("ALERT: ACME above $150"));
        })
        .AssertPassed();

    [Scenario("Unsubscribed observers stop receiving events")]
    [Fact]
    public Task UnsubscribeStopsEvents() =>
        Given("running the stock price demo", () => true)
        .When("the demo executes", _ => ObserverGeneratorDemo.Run())
        .Then("unsubscription confirmation is logged", log =>
        {
            Assert.Contains(log, l => l.Contains("Dashboard unsubscribed"));
            Assert.Contains(log, l => l.Contains("No subscribers: True"));
        })
        .AssertPassed();

    [Scenario("Demo runs without errors")]
    [Fact]
    public Task DemoRunsSuccessfully() =>
        Given("the observer generator demo", () => true)
        .When("the demo is executed", _ => ObserverGeneratorDemo.Run())
        .Then("it produces output without throwing", log =>
        {
            Assert.NotEmpty(log);
        })
        .AssertPassed();
}
