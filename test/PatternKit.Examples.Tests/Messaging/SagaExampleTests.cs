using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class SagaExampleTests
{
    [Scenario("Run UsesGeneratedSagaFactory")]
    [Fact]
    public void Run_UsesGeneratedSagaFactory()
    {
        var summary = SagaExample.Run();

        ScenarioExpect.Equal("order-42", summary.OrderId);
        ScenarioExpect.True(summary.Submitted);
        ScenarioExpect.True(summary.Paid);
        ScenarioExpect.True(summary.Completed);
    }
}
