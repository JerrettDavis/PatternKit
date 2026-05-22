using PatternKit.Messaging;
using PatternKit.Messaging.Consumers;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Consumers;

public sealed class PollingConsumerTests
{
    [Scenario("Poll ReceivesAvailableMessage")]
    [Fact]
    public void Poll_ReceivesAvailableMessage()
    {
        var messages = new Queue<Message<Command>>();
        messages.Enqueue(Message<Command>.Create(new("sku-1", 3)));
        var consumer = PollingConsumer<Command>.Create("inventory-poller")
            .From(_ => messages.Count == 0 ? null : messages.Dequeue())
            .Build();

        var first = consumer.Poll();
        var second = consumer.Poll();

        ScenarioExpect.True(first.Received);
        ScenarioExpect.Equal("inventory-poller", first.ConsumerName);
        ScenarioExpect.Equal("sku-1", first.Message!.Payload.Sku);
        ScenarioExpect.False(second.Received);
        ScenarioExpect.Null(second.Message);
    }

    [Scenario("Poll PassesMessageContextToSource")]
    [Fact]
    public void Poll_PassesMessageContextToSource()
    {
        MessageContext? captured = null;
        var context = MessageContext.Empty.WithItem("tenant", "north");
        var consumer = PollingConsumer<Command>.Create()
            .From(sourceContext =>
            {
                captured = sourceContext;
                return null;
            })
            .Build();

        _ = consumer.Poll(context);

        ScenarioExpect.Same(context, captured);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => PollingConsumer<Command>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => PollingConsumer<Command>.Create().From(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => PollingConsumer<Command>.Create().Build());
    }

    public sealed record Command(string Sku, int Quantity);
}
