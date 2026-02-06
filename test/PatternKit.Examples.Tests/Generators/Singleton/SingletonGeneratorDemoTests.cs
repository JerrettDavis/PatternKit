using PatternKit.Examples.Generators.Singleton;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.Singleton;

[Feature("Singleton Generator Example")]
public sealed class SingletonGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Eager singleton returns the same instance")]
    [Fact]
    public Task Eager_Singleton_Returns_Same_Instance() =>
        Given("the eager singleton AppClock", () => true)
        .When("accessed twice", _ => (a: AppClock.Instance, b: AppClock.Instance))
        .Then("both references are the same instance", result =>
        {
            Assert.Same(result.a, result.b);
            Assert.Equal(result.a.ServiceId, result.b.ServiceId);
        })
        .AssertPassed();

    [Scenario("Lazy singleton returns the same instance")]
    [Fact]
    public Task Lazy_Singleton_Returns_Same_Instance() =>
        Given("the lazy singleton AppConfig", () => true)
        .When("accessed twice", _ => (a: AppConfig.Current, b: AppConfig.Current))
        .Then("both references are the same instance", result =>
        {
            Assert.Same(result.a, result.b);
        })
        .AssertPassed();

    [Scenario("Lazy singleton loads config via factory")]
    [Fact]
    public Task Lazy_Singleton_Loads_Config_Via_Factory() =>
        Given("the lazy singleton AppConfig", () => true)
        .When("the config is accessed", _ => AppConfig.Current)
        .Then("it contains expected configuration values", config =>
        {
            Assert.Equal("PatternKit Demo", config.Get("AppName"));
            Assert.Equal("1.0.0", config.Get("Version"));
            Assert.Equal("Production", config.Get("Environment"));
        })
        .AssertPassed();

    [Scenario("Demo runs without errors")]
    [Fact]
    public Task Demo_Run_Executes_Without_Errors() =>
        Given("the singleton generator demo", () => true)
        .When("the demo is executed", _ => SingletonGeneratorDemo.Run())
        .Then("it produces expected output", log =>
        {
            Assert.NotEmpty(log);
            Assert.Contains(log, l => l.Contains("Same instance: True"));
        })
        .AssertPassed();
}
