using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class MessageEnvelopeExampleTests
{
    [Scenario("Run ReturnsExpectedEnvelopeAndContextMetadata")]
    [Fact]
    public void Run_ReturnsExpectedEnvelopeAndContextMetadata()
    {
        var summary = MessageEnvelopeExample.Run();

        ScenarioExpect.Equal("order-42", summary.OrderId);
        ScenarioExpect.Equal("msg-100", summary.MessageId);
        ScenarioExpect.Equal("order-42", summary.CorrelationId);
        ScenarioExpect.Equal("checkout-7", summary.CausationId);
        ScenarioExpect.Equal("order-42:accepted", summary.IdempotencyKey);
        ScenarioExpect.Equal("application/vnd.patternkit.order+json", summary.ContentType);
        ScenarioExpect.Equal("billing", summary.Route);
        ScenarioExpect.Equal(1, summary.Attempt);
    }
}
