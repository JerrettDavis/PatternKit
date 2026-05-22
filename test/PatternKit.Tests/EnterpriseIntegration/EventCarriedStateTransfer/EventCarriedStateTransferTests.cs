using PatternKit.EnterpriseIntegration.EventCarriedStateTransfer;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.EnterpriseIntegration.EventCarriedStateTransfer;

[Feature("Event-Carried State Transfer")]
public sealed class EventCarriedStateTransferTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Event-carried state transfer extracts keyed state from an event")]
    [Fact]
    public Task Event_Carried_State_Transfer_Extracts_Keyed_State_From_An_Event()
        => Given("an inventory state transfer", CreateTransfer)
        .When("a product stock event is transferred", transfer => transfer.Transfer(new ProductStockChanged("SKU-100", 12, 4)))
        .Then("subscribers receive the state and version without a source service callback", result =>
        {
            ScenarioExpect.True(result.Transferred);
            ScenarioExpect.Equal("inventory-state", result.TransferName);
            ScenarioExpect.Equal("SKU-100", result.Key);
            ScenarioExpect.Equal(4L, result.Version);
            ScenarioExpect.Equal(12, result.State!.QuantityOnHand);
        })
        .AssertPassed();

    [Scenario("Event-carried state transfer reports mapper failures")]
    [Fact]
    public Task Event_Carried_State_Transfer_Reports_Mapper_Failures()
        => Given("a transfer with a failing mapper", () => EventCarriedStateTransfer<ProductStockChanged, string, ProductInventoryState>.Create("inventory-state")
            .WithKey(static evt => evt.Sku)
            .WithVersion(static evt => evt.Sequence)
            .WithState(static _ => throw new InvalidOperationException("bad payload"))
            .Build())
        .When("the event is transferred", transfer => transfer.Transfer(new ProductStockChanged("SKU-100", 12, 4)))
        .Then("the failure is explicit", result =>
        {
            ScenarioExpect.True(result.Failed);
            ScenarioExpect.Equal("bad payload", result.Exception!.Message);
        })
        .AssertPassed();

    [Scenario("Event-carried state transfer validates configuration")]
    [Fact]
    public Task Event_Carried_State_Transfer_Validates_Configuration()
        => Given("invalid transfer configuration", () => true)
        .Then("invalid names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => EventCarriedStateTransfer<ProductStockChanged, string, ProductInventoryState>.Create("")
                .WithKey(static evt => evt.Sku)
                .WithVersion(static evt => evt.Sequence)
                .WithState(ToState)
                .Build()))
        .And("missing selectors are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => EventCarriedStateTransfer<ProductStockChanged, string, ProductInventoryState>.Create().Build()))
        .And("null selectors are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => EventCarriedStateTransfer<ProductStockChanged, string, ProductInventoryState>.Create().WithKey(null!)))
        .And("null events are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateTransfer().Transfer(null!)))
        .AssertPassed();

    private static EventCarriedStateTransfer<ProductStockChanged, string, ProductInventoryState> CreateTransfer()
        => EventCarriedStateTransfer<ProductStockChanged, string, ProductInventoryState>.Create("inventory-state")
            .WithKey(static evt => evt.Sku)
            .WithVersion(static evt => evt.Sequence)
            .WithState(ToState)
            .Build();

    private static ProductInventoryState ToState(ProductStockChanged evt)
        => new(evt.Sku, evt.QuantityOnHand);

    private sealed record ProductStockChanged(string Sku, int QuantityOnHand, long Sequence);

    private sealed record ProductInventoryState(string Sku, int QuantityOnHand);
}
