using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ReliabilityExampleTests
{
    [Scenario("RunAsync DispatchesOneOutboxMessageForDuplicateInput")]
    [Fact]
    public async Task RunAsync_DispatchesOneOutboxMessageForDuplicateInput()
    {
        var dispatched = await ReliabilityExample.RunAsync();

        ScenarioExpect.Equal(["order-42"], dispatched);
    }
}
