using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using Showcase = PatternKit.Examples.PatternShowcase.PatternShowcase;

namespace PatternKit.Examples.Tests.PatternShowcase;

[Feature("Patterns Showcase - Integrated Order Processing (TinyBDD)")]
public sealed class PatternShowcaseTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record State(Showcase.IOrderProcessingFacade Facade, Showcase.OrderDto Dto);

    [Scenario("Card payment with promo items and VIP customer applies percent discount and card fee")]
    [Fact]
    public Task PlaceOrder_Card_Promo_Vip()
        => Given("facade and a card order with promo items",
                () => new State(
                    Showcase.Build(),
                    new Showcase.OrderDto(
                        OrderId: "ORD-1001",
                        CustomerId: "VIP-42",
                        PaymentKind: "card",
                        Items:
                        [
                            new Showcase.OrderItemDto("SKU-1","Widget", 100.00m, 2, "Promo"),
                            new Showcase.OrderItemDto("SKU-2","Addon", 50.00m, 1)
                        ])))
           .When("placing the order", Place)
           .Then("ok == true", r => r.ok)
           .And("message is Order processed", r => r.message == "Order processed")
           .And("total includes 10% promo discount and 2.9% card fee",
                r => r.total == 232.25m) // subtotal=250, discount=25, fee=7.25 → total=232.25
           .AssertPassed();

    [Scenario("Cash payment with VIP customer applies flat discount and no fee")]
    [Fact]
    public Task PlaceOrder_Cash_Vip_NoPromo()
        => Given("facade and a cash order without promo items",
                () => new State(
                    Showcase.Build(),
                    new Showcase.OrderDto(
                        OrderId: "ORD-2002",
                        CustomerId: "VIP-99",
                        PaymentKind: "cash",
                        Items:
                        [
                            new Showcase.OrderItemDto("X","Thing", 80.00m, 1),
                            new Showcase.OrderItemDto("Y","Other", 120.00m, 1)
                        ])))
           .When("placing the order", Place)
           .Then("ok == true", r => r.ok)
           .And("total equals subtotal minus VIP flat 15",
                r => r.total == 185.00m) // subtotal=200, discount=15, fee=0
           .AssertPassed();

    [Scenario("Missing required fields fails adaptation with clear message")]
    [Fact]
    public Task PlaceOrder_Validation_Failure()
        => Given("facade and an invalid order (missing id)",
                () => new State(
                    Showcase.Build(),
                    new Showcase.OrderDto(
                        OrderId: "",
                        CustomerId: "C-1",
                        PaymentKind: "card",
                        Items: [new Showcase.OrderItemDto("S","N", 10m, 1)])))
           .When("placing the order", Place)
           .Then("ok == false", r => r.ok == false)
           .And("validation message is returned", r => r.message.Contains("OrderId required", StringComparison.Ordinal))
           .AssertPassed();

    private static (bool ok, string message, decimal total) Place(State s)
        => s.Facade.Place(s.Dto);
}

