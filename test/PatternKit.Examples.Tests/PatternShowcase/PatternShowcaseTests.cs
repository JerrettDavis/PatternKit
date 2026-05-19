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
                        Items: [new Showcase.OrderItemDto("S", "N", 10m, 1)])))
           .When("placing the order", Place)
           .Then("ok == false", r => r.ok == false)
           .And("validation message is returned", r => r.message.Contains("OrderId required", StringComparison.Ordinal))
           .AssertPassed();

    private static (bool ok, string message, decimal total) Place(State s)
        => s.Facade.Place(s.Dto);

    [Scenario("Empty items list fails validation")]
    [Fact]
    public Task PlaceOrder_EmptyItems_Fails()
        => Given("facade and an order with no items",
                () => new State(
                    Showcase.Build(),
                    new Showcase.OrderDto(
                        OrderId: "ORD-3003",
                        CustomerId: "C-1",
                        PaymentKind: "card",
                        Items: [])))
           .When("placing the order", Place)
           .Then("ok == false", r => r.ok == false)
           .And("message indicates items required", r => r.message.Contains("item", StringComparison.OrdinalIgnoreCase))
           .AssertPassed();

    [Scenario("Card payment with normal customer has 2.9% fee, no discount")]
    [Fact]
    public Task PlaceOrder_Card_NormalCustomer()
        => Given("facade and a card order with normal customer",
                () => new State(
                    Showcase.Build(),
                    new Showcase.OrderDto(
                        OrderId: "ORD-4004",
                        CustomerId: "NORMAL-42",
                        PaymentKind: "card",
                        Items: [new Showcase.OrderItemDto("SKU-1", "Widget", 100.00m, 1)])))
           .When("placing the order", Place)
           .Then("ok == true", r => r.ok)
           .And("total includes 2.9% card fee only",
                r => r.total == 102.90m) // subtotal=100, discount=0, fee=2.90
           .AssertPassed();

    [Scenario("Cash payment with normal customer has no fee and no discount")]
    [Fact]
    public Task PlaceOrder_Cash_NormalCustomer()
        => Given("facade and a cash order with normal customer",
                () => new State(
                    Showcase.Build(),
                    new Showcase.OrderDto(
                        OrderId: "ORD-5005",
                        CustomerId: "NORMAL-99",
                        PaymentKind: "cash",
                        Items: [new Showcase.OrderItemDto("SKU-1", "Widget", 50.00m, 2)])))
           .When("placing the order", Place)
           .Then("ok == true", r => r.ok)
           .And("total equals subtotal (no discount, no fee)",
                r => r.total == 100.00m)
           .AssertPassed();

    [Scenario("Empty payment kind defaults to sandbox and cash payment")]
    [Fact]
    public Task PlaceOrder_DefaultPaymentKind()
        => Given("facade and an order with empty payment kind",
                () => new State(
                    Showcase.Build(),
                    new Showcase.OrderDto(
                        OrderId: "ORD-6006",
                        CustomerId: "C-1",
                        PaymentKind: "",
                        Items: [new Showcase.OrderItemDto("SKU-1", "Widget", 100.00m, 1)])))
           .When("placing the order", Place)
           .Then("ok == true", r => r.ok)
           .And("total equals subtotal (cash, no fee, no discount)",
                r => r.total == 100.00m)
           .AssertPassed();
}

public sealed class PatternShowcaseUnitTests
{
    [Scenario("Build ReturnsNonNull")]
    [Fact]
    public void Build_ReturnsNonNull()
    {
        var facade = Showcase.Build();

        ScenarioExpect.NotNull(facade);
    }

    [Scenario("SandboxGateway ChargeAsync Completes")]
    [Fact]
    public void SandboxGateway_ChargeAsync_Completes()
    {
        var gateway = new Showcase.SandboxGateway();

        var task = gateway.ChargeAsync(100m, CancellationToken.None);

        ScenarioExpect.True(task.IsCompleted);
    }

    [Scenario("SandboxGateway RefundAsync Completes")]
    [Fact]
    public void SandboxGateway_RefundAsync_Completes()
    {
        var gateway = new Showcase.SandboxGateway();

        var task = gateway.RefundAsync(100m, CancellationToken.None);

        ScenarioExpect.True(task.IsCompleted);
    }

    [Scenario("StripeGateway ChargeAsync Completes")]
    [Fact]
    public void StripeGateway_ChargeAsync_Completes()
    {
        var gateway = new Showcase.StripeGateway();

        var task = gateway.ChargeAsync(100m, CancellationToken.None);

        ScenarioExpect.True(task.IsCompleted);
    }

    [Scenario("StripeGateway RefundAsync Completes")]
    [Fact]
    public void StripeGateway_RefundAsync_Completes()
    {
        var gateway = new Showcase.StripeGateway();

        var task = gateway.RefundAsync(100m, CancellationToken.None);

        ScenarioExpect.True(task.IsCompleted);
    }

    [Scenario("OrderDto Record Works")]
    [Fact]
    public void OrderDto_Record_Works()
    {
        var dto = new Showcase.OrderDto("ORD-1", "C-1", "card",
            [new Showcase.OrderItemDto("SKU", "Name", 10m, 1)]);

        ScenarioExpect.Equal("ORD-1", dto.OrderId);
        ScenarioExpect.Equal("C-1", dto.CustomerId);
        ScenarioExpect.Equal("card", dto.PaymentKind);
        ScenarioExpect.Single(dto.Items);
    }

    [Scenario("OrderItemDto Record Works")]
    [Fact]
    public void OrderItemDto_Record_Works()
    {
        var item = new Showcase.OrderItemDto("SKU-1", "Widget", 25.99m, 3, "Electronics");

        ScenarioExpect.Equal("SKU-1", item.Sku);
        ScenarioExpect.Equal("Widget", item.Name);
        ScenarioExpect.Equal(25.99m, item.Price);
        ScenarioExpect.Equal(3, item.Qty);
        ScenarioExpect.Equal("Electronics", item.Category);
    }

    [Scenario("OrderItemDto DefaultCategory IsNull")]
    [Fact]
    public void OrderItemDto_DefaultCategory_IsNull()
    {
        var item = new Showcase.OrderItemDto("SKU-1", "Widget", 25.99m, 3);

        ScenarioExpect.Null(item.Category);
    }

    [Scenario("Cash Record Works")]
    [Fact]
    public void Cash_Record_Works()
    {
        var cash = new Showcase.Cash(50.00m);

        ScenarioExpect.Equal(50.00m, cash.Amount);
    }

    [Scenario("Card Record Works")]
    [Fact]
    public void Card_Record_Works()
    {
        var card = new Showcase.Card("VISA", "4242", 100.00m);

        ScenarioExpect.Equal("VISA", card.Brand);
        ScenarioExpect.Equal("4242", card.Last4);
        ScenarioExpect.Equal(100.00m, card.Amount);
    }

    [Scenario("OrderItem Record Works")]
    [Fact]
    public void OrderItem_Record_Works()
    {
        var item = new Showcase.OrderItem("SKU-1", "Widget", 10m, 5, "Promo");

        ScenarioExpect.Equal("SKU-1", item.Sku);
        ScenarioExpect.Equal("Widget", item.Name);
        ScenarioExpect.Equal(10m, item.UnitPrice);
        ScenarioExpect.Equal(5, item.Quantity);
        ScenarioExpect.Equal("Promo", item.Category);
    }

    [Scenario("OrderContext Total Computation")]
    [Fact]
    public void OrderContext_Total_Computation()
    {
        var order = new Showcase.Order
        {
            OrderId = "ORD-1",
            CustomerId = "C-1",
            Payment = new Showcase.Cash(100m),
            Items = []
        };
        var ctx = new Showcase.OrderContext
        {
            Order = order,
            Gateway = new Showcase.SandboxGateway()
        };

        ctx.Subtotal = 100m;
        ctx.Discount = 10m;
        ctx.Fees = 5m;

        ScenarioExpect.Equal(95m, ctx.Total); // 100 - 10 + 5 = 95
    }

    [Scenario("OrderPlaced Record Works")]
    [Fact]
    public void OrderPlaced_Record_Works()
    {
        var evt = new Showcase.OrderPlaced("ORD-123");

        ScenarioExpect.Equal("ORD-123", evt.OrderId);
    }

    [Scenario("GenerateReceipt Record Works")]
    [Fact]
    public void GenerateReceipt_Record_Works()
    {
        var cmd = new Showcase.GenerateReceipt("ORD-456", 123.45m);

        ScenarioExpect.Equal("ORD-456", cmd.OrderId);
        ScenarioExpect.Equal(123.45m, cmd.Total);
    }
}

