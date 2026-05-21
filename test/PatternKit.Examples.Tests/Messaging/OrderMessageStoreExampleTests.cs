using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Storage;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class OrderMessageStoreExampleTests
{
    [Scenario("FluentMessageStore RecordsReplaySafeEvents")]
    [Fact]
    public void FluentMessageStore_RecordsReplaySafeEvents()
    {
        var summary = OrderMessageStoreExampleRunner.RunFluent(
            new("order-1", "Submitted", 125m, false),
            "msg-1",
            "checkout-1");

        ScenarioExpect.True(summary.Stored);
        ScenarioExpect.False(summary.Duplicate);
        ScenarioExpect.Equal(1, summary.ReplayCount);
        ScenarioExpect.Null(summary.RejectionReason);
    }

    [Scenario("GeneratedMessageStore MatchesFluentRetention")]
    [Fact]
    public void GeneratedMessageStore_MatchesFluentRetention()
    {
        var orderEvent = new OrderMessageStoreEvent("order-1", "PaymentCaptured", 125m, true);
        var fluent = OrderMessageStoreExampleRunner.RunFluent(orderEvent, "msg-1", "checkout-1");
        var generated = GeneratedOrderMessageStore.Create().Append(
            Message<OrderMessageStoreEvent>.Create(orderEvent).WithMessageId("msg-1").WithCorrelationId("checkout-1"));

        ScenarioExpect.False(fluent.Stored);
        ScenarioExpect.False(generated.Stored);
        ScenarioExpect.Equal(fluent.RejectionReason, generated.RejectionReason);
    }

    [Scenario("ServiceCollection ImportsMessageStoreExample")]
    [Fact]
    public void ServiceCollection_ImportsMessageStoreExample()
    {
        var services = new ServiceCollection();
        services.AddOrderMessageStoreDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<MessageStore<OrderMessageStoreEvent>>();
        var runner = provider.GetRequiredService<OrderMessageStoreExampleRunner>();

        var direct = store.Append(Message<OrderMessageStoreEvent>.Create(new("order-1", "Submitted", 125m, false)).WithMessageId("msg-1"));
        var summary = runner.RunGenerated(new("order-1", "Submitted", 125m, false), "msg-2", "checkout-1");

        ScenarioExpect.True(direct.Stored);
        ScenarioExpect.True(summary.Stored);
        ScenarioExpect.Equal(1, summary.ReplayCount);
    }

    [Scenario("AggregateServiceCollection ImportsMessageStoreExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsMessageStoreExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderMessageStoreExampleService>();

        var summary = example.Service.Record(new("order-1", "Submitted", 125m, false), "msg-1", "checkout-1");

        ScenarioExpect.True(summary.Stored);
        ScenarioExpect.Equal(1, summary.ReplayCount);
    }
}
