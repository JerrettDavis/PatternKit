using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ShipmentGuaranteedDeliveryExampleTests
{
    [Scenario("Fluent shipment guaranteed delivery dispatches command")]
    [Fact]
    public async Task FluentShipmentGuaranteedDelivery_DispatchesCommand()
    {
        var summary = await ShipmentGuaranteedDeliveryExampleRunner.RunFluentAsync(new("ship-1", "UPS", "Chicago"));

        ScenarioExpect.Equal("ship-1", summary.ShipmentId);
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Delivered, summary.Status);
        ScenarioExpect.Equal(1, summary.Attempts);
        ScenarioExpect.Null(summary.LastError);
    }

    [Scenario("Fluent shipment guaranteed delivery releases transient failure")]
    [Fact]
    public async Task FluentShipmentGuaranteedDelivery_ReleasesTransientFailure()
    {
        var summary = await ShipmentGuaranteedDeliveryExampleRunner.RunFluentAsync(new("ship-2", "FedEx", "Austin"), transientFailure: true);

        ScenarioExpect.Equal("ship-2", summary.ShipmentId);
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Pending, summary.Status);
        ScenarioExpect.Equal(1, summary.Attempts);
        ScenarioExpect.Equal("carrier API unavailable", summary.LastError);
    }

    [Scenario("Generated shipment guaranteed delivery dispatches command")]
    [Fact]
    public async Task GeneratedShipmentGuaranteedDelivery_DispatchesCommand()
    {
        var service = new ShipmentGuaranteedDeliveryService(GeneratedShipmentGuaranteedDeliveryQueue.Create());

        await service.ScheduleAsync(new("ship-3", "DHL", "Seattle"));
        var summary = await service.DispatchNextAsync();

        ScenarioExpect.Equal("ship-3", summary.ShipmentId);
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Delivered, summary.Status);
    }

    [Scenario("Shipment guaranteed delivery is available through focused DI")]
    [Fact]
    public async Task ShipmentGuaranteedDelivery_IsAvailableThroughFocusedDi()
    {
        var services = new ServiceCollection();
        services.AddShipmentGuaranteedDeliveryDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<ShipmentGuaranteedDeliveryExampleRunner>();

        var summary = await runner.RunGeneratedAsync(new("ship-4", "USPS", "Denver"));

        ScenarioExpect.Equal("ship-4", summary.ShipmentId);
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Delivered, summary.Status);
    }

    [Scenario("Shipment guaranteed delivery is available through aggregate DI")]
    [Fact]
    public async Task ShipmentGuaranteedDelivery_IsAvailableThroughAggregateDi()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<ShipmentGuaranteedDeliveryExampleService>();

        var summary = await example.Runner.RunGeneratedAsync(new("ship-6", "UPS", "Phoenix"));

        ScenarioExpect.Equal("shipment-guaranteed-delivery", example.Queue.Name);
        ScenarioExpect.Equal("ship-6", summary.ShipmentId);
        ScenarioExpect.Equal(GuaranteedDeliveryStatus.Delivered, summary.Status);
    }
}
