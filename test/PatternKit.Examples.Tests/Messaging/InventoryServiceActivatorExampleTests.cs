using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class InventoryServiceActivatorExampleTests
{
    [Scenario("FluentServiceActivator ReservesInventory")]
    [Fact]
    public void FluentServiceActivator_ReservesInventory()
    {
        var summary = InventoryServiceActivatorExampleRunner.RunFluent(new("SKU-100", 5));

        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.True(summary.Reserved);
        ScenarioExpect.Equal("allocated", summary.Reason);
    }

    [Scenario("GeneratedServiceActivator MatchesFluentActivator")]
    [Fact]
    public void GeneratedServiceActivator_MatchesFluentActivator()
    {
        var generated = InventoryServiceActivatorExampleRunner.RunGeneratedStatic(new("SKU-100", 5));
        var fluent = InventoryServiceActivatorExampleRunner.RunFluent(new("SKU-100", 5));

        ScenarioExpect.Equal(fluent.Completed, generated.Completed);
        ScenarioExpect.Equal(fluent.Reserved, generated.Reserved);
        ScenarioExpect.Equal(fluent.Reason, generated.Reason);
    }

    [Scenario("ServiceCollection ImportsServiceActivatorExample")]
    [Fact]
    public void ServiceCollection_ImportsServiceActivatorExample()
    {
        var services = new ServiceCollection();
        services.AddInventoryServiceActivatorDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var service = provider.GetRequiredService<InventoryServiceActivatorService>();

        var summary = service.Reserve(new("SKU-100", 5));

        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.True(summary.Reserved);
    }

    [Scenario("AggregateServiceCollection ImportsServiceActivatorExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsServiceActivatorExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<InventoryServiceActivatorExampleService>();

        var summary = example.Service.Reserve(new("SKU-100", 5));

        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.True(summary.Reserved);
    }
}
