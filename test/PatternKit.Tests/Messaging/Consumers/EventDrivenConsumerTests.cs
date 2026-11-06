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

    [Scenario("Accept UsesMessageContextWhenContextIsNotProvided")]
    [Fact]
    public void Accept_UsesMessageContextWhenContextIsNotProvided()
    {
        MessageContext? captured = null;
        var message = Message<Command>.Create(new("sku-1", 3))
            .WithMessageId("message-1")
            .WithCorrelationId("correlation-1");
        var consumer = EventDrivenConsumer<Command>.Create()
            .Handle("capture", (handledMessage, context) =>
            {
                captured = context;
                ScenarioExpect.Same(message, handledMessage);
            })
            .Build();

        var result = consumer.Accept(message);

        ScenarioExpect.True(result.Accepted);
        ScenarioExpect.Same(message, result.Message);
        ScenarioExpect.Same(message.Headers, captured!.Headers);
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

    [Scenario("Accept ConvertsThrownHandlersToFailures")]
    [Fact]
    public void Accept_ConvertsThrownHandlersToFailures()
    {
        var exception = new InvalidOperationException("handler failed");
        var consumer = EventDrivenConsumer<Command>.Create()
            .Handle("throwing", (_, _) => throw exception)
            .Build();

        var result = consumer.Accept(Message<Command>.Create(new("sku-1", 3)));

        var failure = ScenarioExpect.Single(result.Failures);
        ScenarioExpect.False(result.Accepted);
        ScenarioExpect.Equal(1, result.HandlerCount);
        ScenarioExpect.Equal("throwing", failure.HandlerName);
        ScenarioExpect.Equal("handler failed", failure.Reason);
        ScenarioExpect.Same(exception, failure.Exception);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => EventDrivenConsumer<Command>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() => EventDrivenConsumer<Command>.Create().Handle("", (_, _) => EventDrivenConsumerHandlerResult.Success("handler")));
        ScenarioExpect.Throws<ArgumentNullException>(() => EventDrivenConsumer<Command>.Create().Handle("handler", (EventDrivenConsumer<Command>.Handler)null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => EventDrivenConsumer<Command>.Create().Handle("handler", (Action<Message<Command>, MessageContext>)null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => EventDrivenConsumer<Command>.Create().Build());
    }

    [Scenario("Accept RejectsNullMessage")]
    [Fact]
    public void Accept_RejectsNullMessage()
    {
        var consumer = EventDrivenConsumer<Command>.Create()
            .Handle("handler", (_, _) => EventDrivenConsumerHandlerResult.Success("handler"))
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => consumer.Accept(null!));
    }

    [Scenario("HandlerResult RejectsInvalidInputs")]
    [Fact]
    public void HandlerResult_RejectsInvalidInputs()
    {
        ScenarioExpect.Throws<ArgumentException>(() => EventDrivenConsumerHandlerResult.Success(""));
        ScenarioExpect.Throws<ArgumentException>(() => EventDrivenConsumerHandlerResult.Failure("", "reason"));
        ScenarioExpect.Throws<ArgumentException>(() => EventDrivenConsumerHandlerResult.Failure("handler", ""));
    }

    public sealed record Command(string Sku, int Quantity);
}
