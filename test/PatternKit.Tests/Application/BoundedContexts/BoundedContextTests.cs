using PatternKit.Application.BoundedContexts;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.BoundedContexts;

[Feature("Bounded Context")]
public sealed class BoundedContextTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Builds descriptor with capabilities and adapters")]
    [Fact]
    public Task Builds_Descriptor_With_Capabilities_And_Adapters()
        => Given("a bounded context builder", () => BoundedContextDescriptor.Create("Fulfillment"))
            .When("registering capabilities and translations", builder => builder
                .AddCapability("quote shipment", typeof(IShipmentQuoter))
                .AddCapability("allocate inventory", typeof(IInventoryAllocator))
                .AddAdapter("Catalog", "Fulfillment", typeof(CatalogProduct), typeof(FulfillmentItem))
                .Build())
            .Then("the context name is preserved", descriptor =>
                ScenarioExpect.Equal("Fulfillment", descriptor.Name))
            .And("capabilities are ordered by name", descriptor =>
                ScenarioExpect.Equal(["allocate inventory", "quote shipment"], descriptor.Capabilities.Select(static capability => capability.Name).ToArray()))
            .And("translation metadata captures the upstream and downstream contexts", descriptor =>
            {
                var adapter = ScenarioExpect.Single(descriptor.Adapters);
                ScenarioExpect.Equal("Catalog", adapter.UpstreamContext);
                ScenarioExpect.Equal("Fulfillment", adapter.DownstreamContext);
                ScenarioExpect.Equal(typeof(CatalogProduct), adapter.SourceType);
                ScenarioExpect.Equal(typeof(FulfillmentItem), adapter.TargetType);
            })
            .AssertPassed();

    [Scenario("Rejects invalid bounded context registrations")]
    [Fact]
    public Task Rejects_Invalid_Bounded_Context_Registrations()
        => Given("a bounded context builder", () => BoundedContextDescriptor.Create("Fulfillment"))
            .Then("empty context names are rejected", _ =>
                ScenarioExpect.Throws<ArgumentException>(() => BoundedContextDescriptor.Create(" ")))
            .And("duplicate capabilities are rejected", builder =>
                ScenarioExpect.Throws<InvalidOperationException>(() => builder
                    .AddCapability("quote shipment", typeof(IShipmentQuoter))
                    .AddCapability("quote shipment", typeof(IShipmentQuoter))))
            .And("duplicate adapters are rejected", _ =>
                ScenarioExpect.Throws<InvalidOperationException>(() => BoundedContextDescriptor.Create("Fulfillment")
                    .AddAdapter("Catalog", "Fulfillment", typeof(CatalogProduct), typeof(FulfillmentItem))
                    .AddAdapter("Catalog", "Fulfillment", typeof(CatalogProduct), typeof(FulfillmentItem))))
            .AssertPassed();

    private interface IShipmentQuoter;

    private interface IInventoryAllocator;

    private sealed record CatalogProduct(string Sku);

    private sealed record FulfillmentItem(string Sku);
}
