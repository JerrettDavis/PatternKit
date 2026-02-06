using PatternKit.Structural.Facade;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural.Facade;

[Feature("Structural - TypedFacade<TInterface> (compile-time safe interface-based facade)")]
public sealed class TypedFacadeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Test interfaces
    public interface ICalculator
    {
        int Add(int a, int b);
        int Subtract(int a, int b);
        int Multiply(int a, int b);
    }

    public interface IOrderService
    {
        OrderResult PlaceOrder(OrderRequest request);
        OrderResult CancelOrder(string orderId);
        bool CheckInventory(string productId, int quantity);
    }

    public interface ISimpleService
    {
        string GetStatus();
    }

    public interface IComplexService
    {
        string Operation1(string arg);
        int Operation2(int a, int b);
        bool Operation3(string s, int n, bool flag);
        decimal Operation4(decimal a, decimal b, decimal c, decimal d);
    }

    public sealed record OrderRequest(string ProductId, int Quantity, decimal Price);
    public sealed record OrderResult(bool Success, string? OrderId = null);

    [Scenario("TypedFacade creates compile-time safe interface implementation")]
    [Fact]
    public Task TypedFacade_Creates_Safe_Implementation()
        => Given("a typed facade for ICalculator", () =>
            TypedFacade<ICalculator>.Create()
                .Map(x => x.Add, (int a, int b) => a + b)
                .Map(x => x.Subtract, (int a, int b) => a - b)
                .Map(x => x.Multiply, (int a, int b) => a * b)
                .Build())
            .When("using the facade", calc => (calc.Add(5, 3), calc.Subtract(10, 4), calc.Multiply(6, 7)))
            .Then("add works", r => r.Item1 == 8)
            .And("subtract works", r => r.Item2 == 6)
            .And("multiply works", r => r.Item3 == 42)
            .AssertPassed();

    [Scenario("TypedFacade with complex domain operations")]
    [Fact]
    public Task TypedFacade_Complex_Domain_Operations()
        => Given("an order service facade", () =>
            {
                var orders = new Dictionary<string, OrderRequest>();
                var inventory = new Dictionary<string, int> { ["WIDGET"] = 100, ["GADGET"] = 50 };

                return TypedFacade<IOrderService>.Create()
                    .Map(x => x.PlaceOrder, (OrderRequest req) =>
                    {
                        if (!inventory.TryGetValue(req.ProductId, out var stock) || stock < req.Quantity)
                            return new OrderResult(false);

                        inventory[req.ProductId] -= req.Quantity;
                        var orderId = $"ORD-{Guid.NewGuid():N}";
                        orders[orderId] = req;
                        return new OrderResult(true, orderId);
                    })
                    .Map(x => x.CancelOrder, (string orderId) =>
                    {
                        if (!orders.Remove(orderId, out var order))
                            return new OrderResult(false);

                        inventory[order.ProductId] += order.Quantity;
                        return new OrderResult(true, orderId);
                    })
                    .Map(x => x.CheckInventory, (string productId, int quantity) =>
                        inventory.TryGetValue(productId, out var stock) && stock >= quantity)
                    .Build();
            })
            .When("placing and managing orders", service =>
            {
                var hasStock = service.CheckInventory("WIDGET", 10);
                var order1 = service.PlaceOrder(new OrderRequest("WIDGET", 10, 99.99m));
                var order2 = service.PlaceOrder(new OrderRequest("WIDGET", 200, 99.99m)); // Too many
                var cancelled = service.CancelOrder(order1.OrderId!);
                return (hasStock, order1, order2, cancelled);
            })
            .Then("inventory check succeeds", r => r.hasStock)
            .And("first order succeeds", r => r.order1.Success && r.order1.OrderId != null)
            .And("oversized order fails", r => !r.order2.Success)
            .And("cancellation succeeds", r => r.cancelled.Success)
            .AssertPassed();

    [Scenario("TypedFacade with no parameters")]
    [Fact]
    public Task TypedFacade_No_Parameters()
        => Given("a simple service facade", () =>
            TypedFacade<ISimpleService>.Create()
                .Map(x => x.GetStatus, () => "Operational")
                .Build())
            .When("calling parameterless method", service => service.GetStatus())
            .Then("returns expected value", status => status == "Operational")
            .AssertPassed();

    [Scenario("TypedFacade with varying parameter counts")]
    [Fact]
    public Task TypedFacade_Varying_Parameters()
        => Given("a complex service facade", () =>
            TypedFacade<IComplexService>.Create()
                .Map(x => x.Operation1, (string arg) => $"Result: {arg}")
                .Map(x => x.Operation2, (int a, int b) => a + b)
                .Map(x => x.Operation3, (string s, int n, bool flag) =>
                    flag && s.Length > n)
                .Map(x => x.Operation4, (decimal a, decimal b, decimal c, decimal d) =>
                    a + b + c + d)
                .Build())
            .When("calling methods with different signatures", service => (
                service.Operation1("test"),
                service.Operation2(5, 10),
                service.Operation3("hello", 3, true),
                service.Operation4(1.5m, 2.5m, 3.5m, 4.5m)))
            .Then("1-param method works", r => r.Item1 == "Result: test")
            .And("2-param method works", r => r.Item2 == 15)
            .And("3-param method works", r => r.Item3)
            .And("4-param method works", r => r.Item4 == 12.0m)
            .AssertPassed();

    [Scenario("TypedFacade throws if not all methods are mapped")]
    [Fact]
    public Task TypedFacade_Throws_If_Incomplete()
        => Given("a builder missing method implementations", () =>
            Record.Exception(() =>
                TypedFacade<ICalculator>.Create()
                    .Map(x => x.Add, (int a, int b) => a + b)
                    // Missing Subtract and Multiply
                    .Build()))
            .When("building", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .And("mentions missing methods", ex =>
                ex!.Message.Contains("Subtract") && ex.Message.Contains("Multiply"))
            .AssertPassed();

    [Scenario("TypedFacade throws if method mapped twice")]
    [Fact]
    public Task TypedFacade_Throws_If_Duplicate_Mapping()
        => Given("a builder with duplicate mapping", () =>
            Record.Exception(() =>
                TypedFacade<ICalculator>.Create()
                    .Map(x => x.Add, (int a, int b) => a + b)
                    .Map(x => x.Add, (int a, int b) => a * b) // Duplicate!
                    .Map(x => x.Subtract, (int a, int b) => a - b)
                    .Map(x => x.Multiply, (int a, int b) => a * b)
                    .Build()))
            .When("exception thrown", ex => ex)
            .Then("throws ArgumentException", ex => ex is ArgumentException)
            .And("mentions method name", ex => ex!.Message.Contains("Add"))
            .AssertPassed();

    [Scenario("TypedFacade throws if not an interface")]
    [Fact]
    public Task TypedFacade_Requires_Interface()
        => Given("attempting to create facade from non-interface", () =>
            Record.Exception(TypedFacade<string>.Create))
            .When("exception thrown", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .And("mentions interface requirement", ex => ex!.Message.Contains("interface"))
            .AssertPassed();

    [Scenario("TypedFacade preserves exceptions from handlers")]
    [Fact]
    public Task TypedFacade_Preserves_Exceptions()
        => Given("a facade that throws", () =>
            TypedFacade<ISimpleService>.Create()
                .Map<string>(x => x.GetStatus, () => throw new InvalidOperationException("Test error"))
                .Build())
            .When("calling throwing method", service => Record.Exception(service.GetStatus))
            .Then("exception is preserved", ex => ex is InvalidOperationException)
            .And("message is preserved", ex => ex!.Message == "Test error")
            .AssertPassed();

    [Scenario("TypedFacade is reusable")]
    [Fact]
    public Task TypedFacade_Is_Reusable()
        => Given("a calculator facade", () =>
            {
                var callCount = new[] { 0 }; // Use array to capture by reference
                var calc = TypedFacade<ICalculator>.Create()
                    .Map(x => x.Add, (int a, int b) => { callCount[0]++; return a + b; })
                    .Map(x => x.Subtract, (int a, int b) => a - b)
                    .Map(x => x.Multiply, (int a, int b) => a * b)
                    .Build();
                return (calc, callCount);
            })
            .When("calling multiple times", ctx =>
            {
                ctx.calc.Add(1, 2);
                ctx.calc.Add(3, 4);
                ctx.calc.Add(5, 6);
                return ctx.callCount[0];
            })
            .Then("all calls executed", count => count == 3)
            .AssertPassed();

    [Scenario("TypedFacade with closure captures")]
    [Fact]
    public Task TypedFacade_Captures_Closures()
        => Given("a facade with captured state", () =>
            {
                var log = new List<string>();
                var service = TypedFacade<ISimpleService>.Create()
                    .Map(x => x.GetStatus, () =>
                    {
                        log.Add("called");
                        return "OK";
                    })
                    .Build();
                return (service, log);
            })
            .When("calling method", ctx =>
            {
                ctx.service.GetStatus();
                ctx.service.GetStatus();
                return ctx.log.Count;
            })
            .Then("closure captured state updated", count => count == 2)
            .AssertPassed();

    [Scenario("TypedFacade provides IntelliSense and compile-time safety")]
    [Fact]
    public Task TypedFacade_Compile_Time_Safety()
        => Given("a typed calculator facade", () =>
            TypedFacade<ICalculator>.Create()
                .Map(x => x.Add, (int a, int b) => a + b)
                .Map(x => x.Subtract, (int a, int b) => a - b)
                .Map(x => x.Multiply, (int a, int b) => a * b)
                .Build())
            .When("IDE provides IntelliSense", calc =>
            {
                // This demonstrates compile-time safety:
                // - IDE shows available methods (Add, Subtract, Multiply)
                // - Method signatures are enforced (two ints, returns int)
                // - Typos are caught at compile time
                var result = calc.Add(10, 20); // IntelliSense works here!
                return result;
            })
            .Then("method call is type-safe", result => result == 30)
            .AssertPassed();
}
