using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Channels;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class OrderMessageBusExampleTests
{
    [Scenario("Fluent bus publishes to topic subscribers")]
    [Fact]
    public void Fluent_Bus_Publishes_To_Topic_Subscribers()
    {
        var summary = OrderMessageBusExampleRunner.RunFluent(CreateEvents());

        ScenarioExpect.Equal(1, summary.FulfillmentCount);
        ScenarioExpect.Equal(1, summary.BillingCount);
        ScenarioExpect.Equal(2, summary.AuditCount);
        ScenarioExpect.Equal(["accepted", "paid"], summary.Topics);
    }

    [Scenario("Generated bus matches fluent topology")]
    [Fact]
    public void Generated_Bus_Matches_Fluent_Topology()
    {
        var fluent = OrderMessageBusExampleRunner.RunFluent(CreateEvents());
        var generated = OrderMessageBusExampleRunner.RunGeneratedStatic(CreateEvents());

        ScenarioExpect.Equal(fluent.FulfillmentCount, generated.FulfillmentCount);
        ScenarioExpect.Equal(fluent.BillingCount, generated.BillingCount);
        ScenarioExpect.Equal(fluent.AuditCount, generated.AuditCount);
        ScenarioExpect.Equal(fluent.Topics, generated.Topics);
    }

    [Scenario("ServiceCollection imports message bus example")]
    [Fact]
    public void ServiceCollection_Imports_MessageBusExample()
    {
        var services = new ServiceCollection();
        services.AddOrderMessageBusDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<OrderMessageBusExampleRunner>();
        var summary = runner.RunGenerated(CreateEvents());

        ScenarioExpect.Equal(2, summary.AuditCount);
        ScenarioExpect.NotNull(provider.GetRequiredService<MessageBus<BusOrderEvent>>());
    }

    [Scenario("Aggregate examples import message bus example")]
    [Fact]
    public void Aggregate_Examples_Import_MessageBusExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderMessageBusExampleService>();
        var summary = example.Runner.RunGenerated(CreateEvents());

        ScenarioExpect.Equal(1, summary.FulfillmentCount);
        ScenarioExpect.NotNull(example.Bus);
    }

    private static BusOrderEvent[] CreateEvents()
        =>
        [
            new("O-100", "accepted", 125m),
            new("O-101", "paid", 250m)
        ];
}
