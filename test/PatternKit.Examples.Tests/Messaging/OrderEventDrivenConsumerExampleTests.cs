using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class OrderEventDrivenConsumerExampleTests
{
    [Scenario("FluentEventDrivenConsumer AcceptsOrderEvent")]
    [Fact]
    public void FluentEventDrivenConsumer_AcceptsOrderEvent()
    {
        var summary = OrderEventDrivenConsumerExampleRunner.RunFluent(new("order-1", 42.50m));

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal(1, summary.HandlerCount);
        ScenarioExpect.Equal("accepted:order-1:42.50", ScenarioExpect.Single(summary.AuditEntries));
    }

    [Scenario("GeneratedEventDrivenConsumer MatchesFluentConsumer")]
    [Fact]
    public void GeneratedEventDrivenConsumer_MatchesFluentConsumer()
    {
        var generated = OrderEventDrivenConsumerExampleRunner.RunGeneratedStatic(new("order-1", 42.50m));
        var fluent = OrderEventDrivenConsumerExampleRunner.RunFluent(new("order-1", 42.50m));

        ScenarioExpect.Equal(fluent.Accepted, generated.Accepted);
        ScenarioExpect.Equal(fluent.HandlerCount, generated.HandlerCount);
        ScenarioExpect.Equal(fluent.AuditEntries, generated.AuditEntries);
    }

    [Scenario("ServiceCollection ImportsEventDrivenConsumerExample")]
    [Fact]
    public void ServiceCollection_ImportsEventDrivenConsumerExample()
    {
        var services = new ServiceCollection();
        services.AddOrderEventDrivenConsumerDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var service = provider.GetRequiredService<OrderEventDrivenConsumerService>();

        var summary = service.Accept(new("order-1", 42.50m));

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal("accepted:order-1:42.50", ScenarioExpect.Single(summary.AuditEntries));
    }

    [Scenario("AggregateServiceCollection ImportsEventDrivenConsumerExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsEventDrivenConsumerExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderEventDrivenConsumerExampleService>();

        var summary = example.Service.Accept(new("order-1", 42.50m));

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal("accepted:order-1:42.50", ScenarioExpect.Single(summary.AuditEntries));
    }
}
