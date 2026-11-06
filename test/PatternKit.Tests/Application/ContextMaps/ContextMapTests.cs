using PatternKit.Application.ContextMaps;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.ContextMaps;

[Feature("Context Map")]
public sealed class ContextMapTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Builds context map relationships")]
    [Fact]
    public Task Builds_Context_Map_Relationships()
        => Given("a context map builder", () => ContextMapDescriptor.Create("Commerce"))
            .When("registering context relationships", builder => builder
                .AddRelationship("Catalog", "Fulfillment", ContextRelationshipKind.PublishedLanguage, "ProductFeed")
                .AddRelationship("Fulfillment", "Billing", ContextRelationshipKind.CustomerSupplier, "ShipmentBilling")
                .Build())
            .Then("the map name is preserved", map =>
                ScenarioExpect.Equal("Commerce", map.Name))
            .And("relationships are ordered by upstream context", map =>
                ScenarioExpect.Equal(["Catalog", "Fulfillment"], map.Relationships.Select(static relationship => relationship.UpstreamContext).ToArray()))
            .And("relationship metadata is preserved", map =>
            {
                var relationship = ScenarioExpect.Single(map.Relationships.Where(static item => item.UpstreamContext == "Catalog"));
                ScenarioExpect.Equal("Fulfillment", relationship.DownstreamContext);
                ScenarioExpect.Equal(ContextRelationshipKind.PublishedLanguage, relationship.Kind);
                ScenarioExpect.Equal("ProductFeed", relationship.ContractName);
            })
            .AssertPassed();

    [Scenario("Rejects invalid context map registrations")]
    [Fact]
    public Task Rejects_Invalid_Context_Map_Registrations()
        => Given("a context map builder", () => ContextMapDescriptor.Create("Commerce"))
            .Then("empty map names are rejected", _ =>
                ScenarioExpect.Throws<ArgumentException>(() => ContextMapDescriptor.Create("")))
            .And("empty relationship contracts are rejected", builder =>
                ScenarioExpect.Throws<ArgumentException>(() => builder.AddRelationship("Catalog", "Fulfillment", ContextRelationshipKind.PublishedLanguage, "")))
            .And("duplicate relationships are rejected", _ =>
                ScenarioExpect.Throws<InvalidOperationException>(() => ContextMapDescriptor.Create("Commerce")
                    .AddRelationship("Catalog", "Fulfillment", ContextRelationshipKind.PublishedLanguage, "ProductFeed")
                    .AddRelationship("Catalog", "Fulfillment", ContextRelationshipKind.AntiCorruptionLayer, "ProductFeed")))
            .AssertPassed();
}
