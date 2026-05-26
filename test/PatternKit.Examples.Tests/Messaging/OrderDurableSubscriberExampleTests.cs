using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Consumers;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class OrderDurableSubscriberExampleTests
{
    [Scenario("FluentDurableSubscriber ReplaysStoredShipmentEvents")]
    [Fact]
    public void FluentDurableSubscriber_ReplaysStoredShipmentEvents()
    {
        var summary = OrderDurableSubscriberExampleRunner.RunFluent(CreateEvents());

        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.Equal(2, summary.DeliveredCount);
        ScenarioExpect.Equal(0, summary.SkippedCount);
        ScenarioExpect.Equal(["central:order-1:Packed", "central:order-2:Shipped"], summary.ProjectionEntries);
    }

    [Scenario("GeneratedDurableSubscriber MatchesFluentProjection")]
    [Fact]
    public void GeneratedDurableSubscriber_MatchesFluentProjection()
    {
        var fluent = OrderDurableSubscriberExampleRunner.RunFluent(CreateEvents());
        var generated = OrderDurableSubscriberExampleRunner.RunGeneratedStatic(CreateEvents());

        ScenarioExpect.Equal(fluent.Completed, generated.Completed);
        ScenarioExpect.Equal(fluent.DeliveredCount, generated.DeliveredCount);
        ScenarioExpect.Equal(fluent.ProjectionEntries, generated.ProjectionEntries);
    }

    [Scenario("ServiceCollection ImportsDurableSubscriberExample")]
    [Fact]
    public void ServiceCollection_ImportsDurableSubscriberExample()
    {
        var services = new ServiceCollection();
        services.AddOrderDurableSubscriberDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var subscriber = provider.GetRequiredService<DurableSubscriber<OrderShipmentEvent>>();
        var runner = provider.GetRequiredService<OrderDurableSubscriberExampleRunner>();

        var summary = runner.RunGenerated(CreateEvents());

        ScenarioExpect.NotNull(subscriber);
        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.Equal(2, summary.DeliveredCount);
    }

    [Scenario("AggregateServiceCollection ImportsDurableSubscriberExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsDurableSubscriberExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderDurableSubscriberExampleService>();

        var summary = example.Service.CatchUp(CreateEvents());

        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.Equal(2, summary.DeliveredCount);
    }

    private static OrderShipmentEvent[] CreateEvents()
        => [new("order-1", "Packed", "central"), new("order-2", "Shipped", "central")];
}
