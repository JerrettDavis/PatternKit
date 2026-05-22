using PatternKit.Messaging;
using PatternKit.Messaging.Consumers;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Consumers;

public sealed class EventDrivenConsumerTests
{
    [Scenario("Accept InvokesRegisteredHandlers")]
    [Fact]
    public void Accept_InvokesRegisteredHandlers()
    {
        var handled = new List<string>();
        var consumer = EventDrivenConsumer<Command>.Create("inventory-events")
            .Handle("audit", (message, _) =>
            {
                handled.Add(message.Payload.Sku);
                return EventDrivenConsumerHandlerResult.Success("audit");
            })
            .Build();

        var result = consumer.Accept(Message<Command>.Create(new("sku-1", 3)));

        ScenarioExpect.True(result.Accepted);
        ScenarioExpect.Equal("inventory-events", result.ConsumerName);
        ScenarioExpect.Equal(1, result.HandlerCount);
        ScenarioExpect.Equal("sku-1", ScenarioExpect.Single(handled));
    }

    [Scenario("Accept PassesMessageContextToHandlers")]
    [Fact]
    public void Accept_PassesMessageContextToHandlers()
    {
        MessageContext? captured = null;
        var context = MessageContext.Empty.WithItem("tenant", "north");
        var consumer = EventDrivenConsumer<Command>.Create()
            .Handle("capture", (message, handlerContext) =>
            {
                captured = handlerContext;
                return EventDrivenConsumerHandlerResult.Success("capture");
            })
            .Build();

        _ = consumer.Accept(Message<Command>.Create(new("sku-1", 3)), context);

        ScenarioExpect.Same(context, captured);
    }

    [Scenario("Accept ReportsFailuresAndHonorsErrorPolicy")]
    [Fact]
    public void Accept_ReportsFailuresAndHonorsErrorPolicy()
    {
        var handled = new List<string>();
        var consumer = EventDrivenConsumer<Command>.Create()
            .Handle("reject", (message, _) => EventDrivenConsumerHandlerResult.Failure("reject", "not ready"))
            .Handle("audit", (message, _) =>
            {
                handled.Add(message.Payload.Sku);
                return EventDrivenConsumerHandlerResult.Success("audit");
            })
            .Build();
        var continuing = EventDrivenConsumer<Command>.Create()
            .OnError(EventDrivenConsumerErrorPolicy.Continue)
            .Handle("reject", (message, _) => EventDrivenConsumerHandlerResult.Failure("reject", "not ready"))
            .Handle("audit", (message, _) =>
            {
                handled.Add(message.Payload.Sku);
                return EventDrivenConsumerHandlerResult.Success("audit");
            })
            .Build();

        var stopped = consumer.Accept(Message<Command>.Create(new("sku-1", 3)));
        var continued = continuing.Accept(Message<Command>.Create(new("sku-2", 4)));

        ScenarioExpect.False(stopped.Accepted);
        ScenarioExpect.Equal(1, stopped.HandlerCount);
        ScenarioExpect.Equal("reject", ScenarioExpect.Single(stopped.Failures).HandlerName);
        ScenarioExpect.False(continued.Accepted);
        ScenarioExpect.Equal(2, continued.HandlerCount);
        ScenarioExpect.Equal("sku-2", ScenarioExpect.Single(handled));
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => EventDrivenConsumer<Command>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() => EventDrivenConsumer<Command>.Create().Handle("", (_, _) => EventDrivenConsumerHandlerResult.Success("handler")));
        ScenarioExpect.Throws<ArgumentNullException>(() => EventDrivenConsumer<Command>.Create().Handle("handler", (EventDrivenConsumer<Command>.Handler)null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => EventDrivenConsumer<Command>.Create().Build());
    }

    public sealed record Command(string Sku, int Quantity);
}
