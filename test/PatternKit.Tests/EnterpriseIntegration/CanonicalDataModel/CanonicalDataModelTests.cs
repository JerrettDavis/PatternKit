using PatternKit.EnterpriseIntegration.CanonicalDataModel;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.EnterpriseIntegration.CanonicalDataModel;

[Feature("Canonical Data Model")]
public sealed class CanonicalDataModelTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Canonical data model normalizes different source contracts")]
    [Fact]
    public Task Canonical_Data_Model_Normalizes_Different_Source_Contracts()
        => Given("a canonical order model", CreateModel)
        .When("partner and marketplace orders are normalized", model => new[]
        {
            model.Normalize(new PartnerOrder("P-100", 42.50m, "USD")).Value!,
            model.Normalize(new MarketplaceOrder("M-200", 75_00, "usd")).Value!
        })
        .Then("both contracts become canonical orders", orders =>
        {
            ScenarioExpect.Equal("P-100", orders[0].OrderId);
            ScenarioExpect.Equal(42.50m, orders[0].Total);
            ScenarioExpect.Equal("M-200", orders[1].OrderId);
            ScenarioExpect.Equal(75m, orders[1].Total);
        })
        .AssertPassed();

    [Scenario("Canonical data model reports missing adapters and mapper failures")]
    [Fact]
    public Task Canonical_Data_Model_Reports_Missing_Adapters_And_Mapper_Failures()
        => Given("a canonical model with a failing adapter", () => CanonicalDataModel<CanonicalOrder>.Create("orders")
            .From<PartnerOrder>("partner", static _ => throw new InvalidOperationException("bad partner payload"))
            .Build())
        .When("unsupported and failing sources are normalized", model => new
        {
            Unsupported = model.Normalize(new MarketplaceOrder("M-200", 7500, "USD")),
            Failed = model.Normalize(new PartnerOrder("P-100", 42m, "USD"))
        })
        .Then("both failures are explicit results", result =>
        {
            ScenarioExpect.True(result.Unsupported.Failed);
            ScenarioExpect.True(result.Failed.Failed);
            ScenarioExpect.Contains("No canonical data model adapter", result.Unsupported.Exception!.Message);
            ScenarioExpect.Equal("bad partner payload", result.Failed.Exception!.Message);
        })
        .AssertPassed();

    [Scenario("Canonical data model validates configuration")]
    [Fact]
    public Task Canonical_Data_Model_Validates_Configuration()
        => Given("invalid canonical model inputs", () => true)
        .Then("invalid model names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => CanonicalDataModel<CanonicalOrder>.Create("").From<PartnerOrder>("partner", ToCanonical).Build()))
        .And("missing adapters are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => CanonicalDataModel<CanonicalOrder>.Create().Build()))
        .And("invalid adapter names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => CanonicalDataModel<CanonicalOrder>.Create().From<PartnerOrder>("", ToCanonical)))
        .And("null mappers are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CanonicalDataModel<CanonicalOrder>.Create().From<PartnerOrder>("partner", null!)))
        .And("duplicate source adapters are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => CanonicalDataModel<CanonicalOrder>.Create().From<PartnerOrder>("partner", ToCanonical).From<PartnerOrder>("partner2", ToCanonical)))
        .And("null source values are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateModel().Normalize<PartnerOrder>(null!)))
        .AssertPassed();

    private static CanonicalDataModel<CanonicalOrder> CreateModel()
        => CanonicalDataModel<CanonicalOrder>.Create("commerce-orders")
            .From<PartnerOrder>("partner-order", ToCanonical)
            .From<MarketplaceOrder>("marketplace-order", static order => new(order.Id, order.AmountInCents / 100m, order.Currency.ToUpperInvariant()))
            .Build();

    private static CanonicalOrder ToCanonical(PartnerOrder order)
        => new(order.ExternalId, order.Amount, order.Currency.ToUpperInvariant());

    private sealed record PartnerOrder(string ExternalId, decimal Amount, string Currency);

    private sealed record MarketplaceOrder(string Id, int AmountInCents, string Currency);

    private sealed record CanonicalOrder(string OrderId, decimal Total, string Currency);
}
