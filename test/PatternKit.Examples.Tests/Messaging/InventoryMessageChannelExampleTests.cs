using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class InventoryMessageChannelExampleTests
{
    [Scenario("FluentMessageChannel ProcessesInventoryAdjustment")]
    [Fact]
    public void FluentMessageChannel_ProcessesInventoryAdjustment()
    {
        var summary = InventoryMessageChannelExampleRunner.RunFluent(new("sku-1", 3, "cycle-count"));

        ScenarioExpect.Equal("sku-1", summary.ReceivedSku);
        ScenarioExpect.Equal(0, summary.RemainingMessages);
    }

    [Scenario("GeneratedMessageChannel MatchesFluentProcessing")]
    [Fact]
    public void GeneratedMessageChannel_MatchesFluentProcessing()
    {
        var adjustment = new InventoryAdjustment("sku-1", 3, "cycle-count");
        var fluent = InventoryMessageChannelExampleRunner.RunFluent(adjustment);
        var generated = new InventoryMessageChannelExampleRunner(new(GeneratedInventoryMessageChannel.Create())).RunGenerated(adjustment);

        ScenarioExpect.Equal(fluent.ReceivedSku, generated.ReceivedSku);
        ScenarioExpect.Equal(fluent.RemainingMessages, generated.RemainingMessages);
    }

    [Scenario("ServiceCollection ImportsMessageChannelExample")]
    [Fact]
    public void ServiceCollection_ImportsMessageChannelExample()
    {
        var services = new ServiceCollection();
        services.AddInventoryMessageChannelDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var service = provider.GetRequiredService<InventoryMessageChannelService>();

        var enqueued = service.Enqueue(new("sku-1", 3, "cycle-count"));
        var processed = service.TryProcessNext();

        ScenarioExpect.True(enqueued.Accepted);
        ScenarioExpect.Equal("sku-1", processed.ReceivedSku);
    }

    [Scenario("AggregateServiceCollection ImportsMessageChannelExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsMessageChannelExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<InventoryMessageChannelExampleService>();

        example.Service.Enqueue(new("sku-1", 3, "cycle-count"));
        var processed = example.Service.TryProcessNext();

        ScenarioExpect.Equal("sku-1", processed.ReceivedSku);
    }
}
