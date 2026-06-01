using PatternKit.Behavioral.NullObject;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.NullObject;

[Feature("Behavioral - Null Object<TContract>")]
public sealed class NullObjectTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent Null Object exposes a stable fallback collaborator")]
    [Fact]
    public Task Fluent_Null_Object_Exposes_A_Stable_Fallback_Collaborator()
        => Given("a null object wrapper around a notification sink", () =>
                NullObject<INotificationSink>.Create(new SilentNotificationSink()).Build())
            .When("resolving the fallback twice", wrapper => (First: wrapper.Instance, Second: wrapper.Instance))
            .Then("the fallback is stable and safe to call", resolved =>
            {
                ScenarioExpect.Same(resolved.First, resolved.Second);
                ScenarioExpect.False(resolved.First.Send("order-1", "ignored"));
            })
            .AssertPassed();

    [Scenario("Fluent Null Object validates null inputs")]
    [Fact]
    public Task Fluent_Null_Object_Validates_Null_Inputs()
        => Given("invalid null object inputs", () => true)
            .When("creating builders", _ => new
            {
                MissingInstance = ScenarioExpect.Throws<ArgumentNullException>(() => NullObject<INotificationSink>.Create((INotificationSink)null!)),
                MissingFactory = ScenarioExpect.Throws<ArgumentNullException>(() => NullObject<INotificationSink>.Create((Func<INotificationSink>)null!)),
                MissingFactoryResult = ScenarioExpect.Throws<ArgumentNullException>(() => NullObject<INotificationSink>.Create(static () => null!).Build())
            })
            .Then("arguments are rejected explicitly", errors =>
            {
                ScenarioExpect.Equal("instance", errors.MissingInstance.ParamName);
                ScenarioExpect.Equal("factory", errors.MissingFactory.ParamName);
                ScenarioExpect.Equal("instance", errors.MissingFactoryResult.ParamName);
            })
            .AssertPassed();

    private interface INotificationSink
    {
        bool Send(string recipient, string body);
    }

    private sealed class SilentNotificationSink : INotificationSink
    {
        public bool Send(string recipient, string body) => false;
    }
}
