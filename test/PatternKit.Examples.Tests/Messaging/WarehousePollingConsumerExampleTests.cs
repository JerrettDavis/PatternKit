using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class WarehousePollingConsumerExampleTests
{
    [Scenario("FluentPollingConsumer PullsReplenishmentRequest")]
    [Fact]
    public void FluentPollingConsumer_PullsReplenishmentRequest()
    {
        var summary = WarehousePollingConsumerExampleRunner.RunFluent(new("sku-1", 4));

        ScenarioExpect.True(summary.Received);
        ScenarioExpect.Equal("sku-1", summary.Sku);
    }

    [Scenario("GeneratedPollingConsumer MatchesFluentPolling")]
    [Fact]
    public void GeneratedPollingConsumer_MatchesFluentPolling()
    {
        GeneratedWarehousePollingConsumer.Enqueue(new("sku-1", 4));
        var generated = new WarehousePollingConsumerService(GeneratedWarehousePollingConsumer.Create()).Poll();
        var fluent = WarehousePollingConsumerExampleRunner.RunFluent(new("sku-1", 4));

        ScenarioExpect.Equal(fluent.Received, generated.Received);
        ScenarioExpect.Equal(fluent.Sku, generated.Sku);
    }

    [Scenario("ServiceCollection ImportsPollingConsumerExample")]
    [Fact]
    public void ServiceCollection_ImportsPollingConsumerExample()
    {
        var services = new ServiceCollection();
        services.AddWarehousePollingConsumerDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        GeneratedWarehousePollingConsumer.Enqueue(new("sku-1", 4));
        var service = provider.GetRequiredService<WarehousePollingConsumerService>();

        var summary = service.Poll();

        ScenarioExpect.True(summary.Received);
        ScenarioExpect.Equal("sku-1", summary.Sku);
    }

    [Scenario("AggregateServiceCollection ImportsPollingConsumerExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsPollingConsumerExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        GeneratedWarehousePollingConsumer.Enqueue(new("sku-1", 4));
        var example = provider.GetRequiredService<WarehousePollingConsumerExampleService>();

        var summary = example.Service.Poll();

        ScenarioExpect.Equal("sku-1", summary.Sku);
    }
}
