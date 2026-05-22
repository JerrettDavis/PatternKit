using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ShipmentResequencerExampleTests
{
    [Scenario("FluentResequencer ReleasesBufferedShipmentsInOrder")]
    [Fact]
    public void FluentResequencer_ReleasesBufferedShipmentsInOrder()
    {
        var summaries = ShipmentResequencerExampleRunner.RunFluent(
            new ShipmentEvent(3, "ship-1", "Loaded"),
            new ShipmentEvent(1, "ship-1", "Packed"),
            new ShipmentEvent(2, "ship-1", "Allocated"));

        ScenarioExpect.Equal(0, summaries[0].ReleasedCount);
        ScenarioExpect.Equal(1, summaries[1].ReleasedCount);
        ScenarioExpect.Equal(2, summaries[2].ReleasedCount);
        ScenarioExpect.Equal(new[] { "Allocated", "Loaded" }, summaries[2].ReleasedStatuses);
        ScenarioExpect.Equal(4, summaries[2].NextExpectedSequence);
    }

    [Scenario("GeneratedResequencer MatchesFluentSequencing")]
    [Fact]
    public void GeneratedResequencer_MatchesFluentSequencing()
    {
        var generated = new ShipmentResequencerService(GeneratedShipmentResequencer.Create());
        var fluent = new ShipmentResequencerService(ShipmentResequencers.Create());

        var generatedFirst = generated.Record(new(2, "ship-1", "Allocated"));
        var generatedSecond = generated.Record(new(1, "ship-1", "Packed"));
        var fluentFirst = fluent.Record(new(2, "ship-1", "Allocated"));
        var fluentSecond = fluent.Record(new(1, "ship-1", "Packed"));

        ScenarioExpect.Equal(fluentFirst.ReleasedCount, generatedFirst.ReleasedCount);
        ScenarioExpect.Equal(fluentSecond.ReleasedCount, generatedSecond.ReleasedCount);
        ScenarioExpect.Equal(fluentSecond.NextExpectedSequence, generatedSecond.NextExpectedSequence);
        ScenarioExpect.Equal(fluentSecond.ReleasedStatuses, generatedSecond.ReleasedStatuses);
    }

    [Scenario("ServiceCollection ImportsResequencerExample")]
    [Fact]
    public void ServiceCollection_ImportsResequencerExample()
    {
        var services = new ServiceCollection();
        services.AddShipmentResequencerDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var service = provider.GetRequiredService<ShipmentResequencerService>();

        var summary = service.Record(new(1, "ship-1", "Packed"));

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal("Packed", ScenarioExpect.Single(summary.ReleasedStatuses));
    }

    [Scenario("AggregateServiceCollection ImportsResequencerExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsResequencerExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<ShipmentResequencerExampleService>();

        var summary = example.Service.Record(new(1, "ship-1", "Packed"));

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal("Packed", ScenarioExpect.Single(summary.ReleasedStatuses));
    }
}
