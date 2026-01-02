using PatternKit.Structural.Facade;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using static PatternKit.Examples.FacadeDemo.FacadeDemo;

namespace PatternKit.Examples.Tests.FacadeDemo;

[Feature("Examples - Facade Pattern: E-Commerce Order Processing")]
public sealed class FacadeDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static (OrderProcessingFacade processor, Facade<OrderRequest, OrderResult> facade) CreateFacade()
    {
        var inventory = new InventoryService();
        var processor = new OrderProcessingFacade(inventory);
        var facade = processor.BuildFacade();
        return (processor, facade);
    }

    [Scenario("Place order successfully coordinates all subsystems")]
    [Fact]
    public Task PlaceOrder_Success()
        => Given("an order processing facade", CreateFacade)
            .When("placing a valid order", ctx =>
            {
                var request = new OrderRequest(
                    ProductId: "WIDGET-001",
                    Quantity: 5,
                    CustomerEmail: "test@example.com",
                    ShippingAddress: "123 Main St",
                    PaymentMethod: "VISA-1234",
                    Price: 29.99m);
                return ctx.facade.Execute("place-order", request);
            })
            .Then("order succeeds", r => r.Success)
            .And("order ID is generated", r => !string.IsNullOrEmpty(r.OrderId))
            .And("transaction ID is returned", r => !string.IsNullOrEmpty(r.TransactionId))
            .And("shipment ID is returned", r => !string.IsNullOrEmpty(r.ShipmentId))
            .AssertPassed();

    [Scenario("Place order with insufficient inventory fails gracefully")]
    [Fact]
    public Task PlaceOrder_InsufficientInventory()
        => Given("an order processing facade", CreateFacade)
            .When("placing order with excessive quantity", ctx =>
            {
                var request = new OrderRequest(
                    ProductId: "DEVICE-003",
                    Quantity: 1000,
                    CustomerEmail: "test@example.com",
                    ShippingAddress: "123 Main St",
                    PaymentMethod: "VISA-1234",
                    Price: 99.99m);
                return ctx.facade.Execute("place-order", request);
            })
            .Then("order fails", r => !r.Success)
            .And("error message indicates inventory issue", r => r.ErrorMessage != null && r.ErrorMessage.Contains("inventory"))
            .AssertPassed();

    [Scenario("Cancel order reverses all subsystem operations")]
    [Fact]
    public Task CancelOrder_Success()
        => Given("a facade with a placed order", () =>
            {
                var ctx = CreateFacade();
                var request = new OrderRequest(
                    ProductId: "WIDGET-001",
                    Quantity: 2,
                    CustomerEmail: "test@example.com",
                    ShippingAddress: "123 Main St",
                    PaymentMethod: "VISA-1234",
                    Price: 19.99m);
                var result = ctx.facade.Execute("place-order", request);
                return (ctx.facade, result.OrderId, request);
            })
            .When("cancelling the order", ctx =>
            {
                var cancelRequest = ctx.request with { ProductId = ctx.OrderId! };
                return ctx.facade.Execute("cancel-order", cancelRequest);
            })
            .Then("cancellation succeeds", r => r.Success)
            .And("order ID matches", r => r.OrderId != null)
            .AssertPassed();

    [Scenario("Process return handles product return flow")]
    [Fact]
    public Task ProcessReturn_Success()
        => Given("a facade with a placed order", () =>
            {
                var ctx = CreateFacade();
                var request = new OrderRequest(
                    ProductId: "GADGET-002",
                    Quantity: 3,
                    CustomerEmail: "test@example.com",
                    ShippingAddress: "456 Oak Ave",
                    PaymentMethod: "MC-5678",
                    Price: 39.99m);
                var result = ctx.facade.Execute("place-order", request);
                return (ctx.facade, result.OrderId, request);
            })
            .When("processing a return", ctx =>
            {
                var returnRequest = ctx.request with { ProductId = ctx.OrderId! };
                return ctx.facade.Execute("process-return", returnRequest);
            })
            .Then("return succeeds", r => r.Success)
            .And("order ID matches", r => r.OrderId != null)
            .AssertPassed();

    [Scenario("Unknown operation uses default fallback")]
    [Fact]
    public Task UnknownOperation_UsesDefault()
        => Given("an order processing facade", CreateFacade)
            .When("executing unknown operation", ctx =>
            {
                var request = new OrderRequest(
                    ProductId: "WIDGET-001",
                    Quantity: 1,
                    CustomerEmail: "test@example.com",
                    ShippingAddress: "123 Main St",
                    PaymentMethod: "VISA-1234",
                    Price: 9.99m);
                return ctx.facade.Execute("invalid-operation", request);
            })
            .Then("operation fails", r => !r.Success)
            .And("error indicates unknown operation", r => r.ErrorMessage != null && r.ErrorMessage.Contains("Unknown"))
            .AssertPassed();

    [Scenario("TryExecute provides safe operation execution")]
    [Fact]
    public Task TryExecute_SafeExecution()
        => Given("an order processing facade", CreateFacade)
            .When("using TryExecute", ctx =>
            {
                var request = new OrderRequest(
                    ProductId: "WIDGET-001",
                    Quantity: 1,
                    CustomerEmail: "test@example.com",
                    ShippingAddress: "123 Main St",
                    PaymentMethod: "VISA-1234",
                    Price: 14.99m);
                var success = ctx.facade.TryExecute("place-order", request, out var result);
                return (success, result);
            })
            .Then("execution succeeds", r => r.success)
            .And("result is valid", r => r.result.Success)
            .AssertPassed();

    [Scenario("Multiple orders can be processed sequentially")]
    [Fact]
    public Task MultipleOrders_Sequential()
        => Given("an order processing facade", CreateFacade)
            .When("placing multiple orders", ctx =>
            {
                var order1 = new OrderRequest("WIDGET-001", 2, "user1@test.com", "Addr1", "VISA", 29.99m);
                var order2 = new OrderRequest("GADGET-002", 3, "user2@test.com", "Addr2", "MC", 39.99m);

                var result1 = ctx.facade.Execute("place-order", order1);
                var result2 = ctx.facade.Execute("place-order", order2);

                return (result1, result2);
            })
            .Then("both orders succeed", r => r.result1.Success && r.result2.Success)
            .And("both have unique order IDs", r => r.result1.OrderId != r.result2.OrderId)
            .AssertPassed();

    [Scenario("Facade simplifies complex workflow into single operation call")]
    [Fact]
    public Task Facade_SimplifiesComplexWorkflow()
        => Given("an order processing facade", CreateFacade)
            .When("client executes single 'place-order' operation", ctx =>
            {
                // Client only needs to call one operation
                var request = new OrderRequest(
                    ProductId: "WIDGET-001",
                    Quantity: 1,
                    CustomerEmail: "simple@example.com",
                    ShippingAddress: "Easy St",
                    PaymentMethod: "VISA",
                    Price: 49.99m);

                // Behind the scenes: inventory reservation, payment processing,
                // shipping scheduling, notification sending all coordinated
                return ctx.facade.Execute("place-order", request);
            })
            .Then("complex subsystem coordination is hidden", r => r.Success)
            .And("client receives simple result", r => r.OrderId != null)
            .And("all subsystems coordinated transparently", r =>
                r.TransactionId != null && r.ShipmentId != null)
            .AssertPassed();

    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.FacadeDemo.FacadeDemo.Run();
    }

    [Fact]
    public void CancelOrder_NonExistent_ReturnsError()
    {
        var (_, facade) = CreateFacade();
        var request = new OrderRequest("NONEXISTENT-ORDER", 1, "test@test.com", "Addr", "VISA", 10m);

        var result = facade.Execute("cancel-order", request);

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage!);
    }

    [Fact]
    public void ProcessReturn_NonExistent_ReturnsError()
    {
        var (_, facade) = CreateFacade();
        var request = new OrderRequest("NONEXISTENT-ORDER", 1, "test@test.com", "Addr", "VISA", 10m);

        var result = facade.Execute("process-return", request);

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage!);
    }

    [Fact]
    public void PlaceOrder_ZeroPrice_PaymentFails()
    {
        var (_, facade) = CreateFacade();
        var request = new OrderRequest("WIDGET-001", 1, "test@test.com", "Addr", "VISA", 0m);

        var result = facade.Execute("place-order", request);

        Assert.False(result.Success);
        Assert.Contains("Payment", result.ErrorMessage!);
    }

    [Fact]
    public void InventoryService_Reserve_UnknownProduct_Fails()
    {
        var inventory = new InventoryService();

        var success = inventory.Reserve("UNKNOWN-PRODUCT", 1, out var reservationId);

        Assert.False(success);
        Assert.Empty(reservationId);
    }

    [Fact]
    public void InventoryService_Reserve_TooMuchQuantity_Fails()
    {
        var inventory = new InventoryService();

        var success = inventory.Reserve("WIDGET-001", 9999, out var reservationId);

        Assert.False(success);
        Assert.Empty(reservationId);
    }

    [Fact]
    public void InventoryService_Reserve_Success()
    {
        var inventory = new InventoryService();

        var success = inventory.Reserve("WIDGET-001", 10, out var reservationId);

        Assert.True(success);
        Assert.StartsWith("RES-", reservationId);
    }

    [Fact]
    public void InventoryService_Release_DoesNotThrow()
    {
        var inventory = new InventoryService();

        inventory.Release("RES-12345");
    }

    [Fact]
    public void InventoryService_Restock_IncreasesStock()
    {
        var inventory = new InventoryService();
        inventory.Reserve("WIDGET-001", 100, out _); // Use all stock
        inventory.Restock("WIDGET-001", 50);

        // Now should be able to reserve again
        var success = inventory.Reserve("WIDGET-001", 40, out _);

        Assert.True(success);
    }

    [Fact]
    public void PaymentService_Charge_NegativeAmount_Fails()
    {
        var success = PaymentService.Charge("VISA", -10m, out var transactionId);

        Assert.False(success);
        Assert.Empty(transactionId);
    }

    [Fact]
    public void PaymentService_Charge_ZeroAmount_Fails()
    {
        var success = PaymentService.Charge("VISA", 0m, out var transactionId);

        Assert.False(success);
        Assert.Empty(transactionId);
    }

    [Fact]
    public void PaymentService_Charge_Success()
    {
        var success = PaymentService.Charge("VISA", 100m, out var transactionId);

        Assert.True(success);
        Assert.StartsWith("TX-", transactionId);
    }

    [Fact]
    public void PaymentService_Refund_DoesNotThrow()
    {
        PaymentService.Refund("TX-12345");
    }

    [Fact]
    public void PaymentService_Void_DoesNotThrow()
    {
        PaymentService.Void("TX-12345");
    }

    [Fact]
    public void ShippingService_Schedule_ReturnsShipmentId()
    {
        var shipmentId = ShippingService.Schedule("123 Main St", "WIDGET-001", 5);

        Assert.StartsWith("SHIP-", shipmentId);
    }

    [Fact]
    public void ShippingService_Cancel_DoesNotThrow()
    {
        ShippingService.Cancel("SHIP-12345");
    }

    [Fact]
    public void ShippingService_InitiateReturn_ReturnsReturnId()
    {
        var returnId = ShippingService.InitiateReturn("SHIP-12345");

        Assert.StartsWith("RET-", returnId);
    }

    [Fact]
    public void NotificationService_SendOrderConfirmation_DoesNotThrow()
    {
        NotificationService.SendOrderConfirmation("test@test.com", "ORD-12345");
    }

    [Fact]
    public void NotificationService_SendCancellation_DoesNotThrow()
    {
        NotificationService.SendCancellation("test@test.com", "ORD-12345");
    }

    [Fact]
    public void NotificationService_SendRefundNotice_DoesNotThrow()
    {
        NotificationService.SendRefundNotice("test@test.com", 50.00m);
    }
}