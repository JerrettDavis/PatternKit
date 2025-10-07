using PatternKit.Structural.Facade;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural.Facade;

[Feature("Structural - Facade<TIn,TOut> (simplified subsystem interface)")]
public sealed class FacadeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Simulated subsystems
    private sealed class InventoryService
    {
        public List<string> Log { get; } = [];

        public void Reserve(string item) => Log.Add($"reserve:{item}");

        public void Release(string item) => Log.Add($"release:{item}");
    }

    private sealed class PaymentService
    {
        public List<string> Log { get; } = [];

        public string Charge(decimal amount)
        {
            Log.Add($"charge:{amount}");
            return $"tx-{amount}";
        }

        public void Refund(string txId) => Log.Add($"refund:{txId}");
    }

    private sealed class ShippingService
    {
        public List<string> Log { get; } = [];

        public string Schedule(string address)
        {
            Log.Add($"ship:{address}");
            return $"ship-{address}";
        }
    }

    private sealed record OrderRequest(string Item, decimal Amount, string Address);

    private sealed record OrderResult(string Status, string? TxId = null, string? ShipId = null);

    [Scenario("Single operation executes subsystem coordination")]
    [Fact]
    public Task Single_Operation_Executes()
        => Given("a facade with one operation", () =>
            {
                var inventory = new InventoryService();
                var payment = new PaymentService();
                var shipping = new ShippingService();

                var facade = Facade<OrderRequest, OrderResult>.Create()
                    .Operation("process", (in req) =>
                    {
                        inventory.Reserve(req.Item);
                        var txId = payment.Charge(req.Amount);
                        var shipId = shipping.Schedule(req.Address);
                        return new OrderResult("Processed", txId, shipId);
                    })
                    .Build();

                return (facade, inventory, payment, shipping);
            })
            .When("execute 'process' operation", ctx =>
            {
                var result = ctx.facade.Execute("process", new OrderRequest("Widget", 99.99m, "123 Main"));
                return (result, ctx.inventory, ctx.payment, ctx.shipping);
            })
            .Then("result shows processed", r => r.result.Status == "Processed")
            .And("inventory called", r => r.inventory.Log.Contains("reserve:Widget"))
            .And("payment called", r => r.payment.Log.Contains("charge:99.99"))
            .And("shipping called", r => r.shipping.Log.Contains("ship:123 Main"))
            .And("transaction ID returned", r => r.result.TxId == "tx-99.99")
            .And("shipment ID returned", r => r.result.ShipId == "ship-123 Main")
            .AssertPassed();

    [Scenario("Multiple operations with different subsystem coordination")]
    [Fact]
    public Task Multiple_Operations_Different_Flows()
        => Given("a facade with process and cancel operations", () =>
            {
                var inventory = new InventoryService();
                var payment = new PaymentService();

                var facade = Facade<OrderRequest, OrderResult>.Create()
                    .Operation("process", (in req) =>
                    {
                        inventory.Reserve(req.Item);
                        var txId = payment.Charge(req.Amount);
                        return new OrderResult("Processed", txId);
                    })
                    .Operation("cancel", (in req) =>
                    {
                        inventory.Release(req.Item);
                        payment.Refund($"tx-{req.Amount}");
                        return new OrderResult("Cancelled");
                    })
                    .Build();

                return (facade, inventory, payment);
            })
            .When("execute both operations", ctx =>
            {
                var req = new OrderRequest("Widget", 50m, "456 Oak");
                ctx.facade.Execute("process", req);
                ctx.facade.Execute("cancel", req);
                return (ctx.inventory, ctx.payment);
            })
            .Then("inventory shows reserve then release", r =>
                r.inventory.Log is ["reserve:Widget", "release:Widget"])
            .And("payment shows charge then refund", r =>
                r.payment.Log is ["charge:50", "refund:tx-50"])
            .AssertPassed();

    [Scenario("Unknown operation with default returns default result")]
    [Fact]
    public Task Unknown_Operation_Uses_Default()
        => Given("a facade with default operation", () =>
                Facade<string, string>.Create()
                    .Operation("greet", (in name) => $"Hello, {name}")
                    .Default((in _) => "Unknown operation")
                    .Build())
            .When("execute unknown operation", f => f.Execute("unknown", "World"))
            .Then("returns default result", r => r == "Unknown operation")
            .AssertPassed();

    [Scenario("Unknown operation without default throws")]
    [Fact]
    public Task Unknown_Operation_No_Default_Throws()
        => Given("a facade without default", () =>
                Facade<int, int>.Create()
                    .Operation("double", (in x) => x * 2)
                    .Build())
            .When("execute unknown operation", f => Record.Exception(() => f.Execute("unknown", 5)))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .And("message mentions operation not found", ex => ex!.Message.Contains("unknown") && ex.Message.Contains("not found"))
            .AssertPassed();

    [Scenario("TryExecute returns true for existing operation")]
    [Fact]
    public Task TryExecute_Existing_Returns_True()
        => Given("a facade with operations", () =>
                Facade<int, int>.Create()
                    .Operation("triple", (in x) => x * 3)
                    .Build())
            .When("TryExecute existing operation", f =>
            {
                var success = f.TryExecute("triple", 7, out var result);
                return (success, result);
            })
            .Then("returns true", r => r.success)
            .And("output is correct", r => r.result == 21)
            .AssertPassed();

    [Scenario("TryExecute returns false for unknown operation without default")]
    [Fact]
    public Task TryExecute_Unknown_No_Default_Returns_False()
        => Given("a facade without default", () =>
                Facade<int, int>.Create()
                    .Operation("add", (in x) => x + 1)
                    .Build())
            .When("TryExecute unknown operation", f =>
            {
                var success = f.TryExecute("unknown", 5, out var result);
                return (success, result);
            })
            .Then("returns false", r => !r.success)
            .And("output is default", r => r.result == 0)
            .AssertPassed();

    [Scenario("TryExecute returns true for unknown operation with default")]
    [Fact]
    public Task TryExecute_Unknown_With_Default_Returns_True()
        => Given("a facade with default", () =>
                Facade<int, int>.Create()
                    .Operation("double", (in x) => x * 2)
                    .Default((in _) => -1)
                    .Build())
            .When("TryExecute unknown operation", f =>
            {
                var success = f.TryExecute("unknown", 5, out var result);
                return (success, result);
            })
            .Then("returns true", r => r.success)
            .And("output is default result", r => r.result == -1)
            .AssertPassed();

    [Scenario("HasOperation returns true for registered operations")]
    [Fact]
    public Task HasOperation_Registered_Returns_True()
        => Given("a facade with operations", () =>
                Facade<int, int>.Create()
                    .Operation("add", (in x) => x + 1)
                    .Operation("multiply", (in x) => x * 2)
                    .Build())
            .When("check for registered operation", f => (f.HasOperation("add"), f.HasOperation("multiply"), f.HasOperation("unknown")))
            .Then("add exists", r => r.Item1)
            .And("multiply exists", r => r.Item2)
            .And("unknown does not exist", r => !r.Item3)
            .AssertPassed();

    [Scenario("Duplicate operation names throw on build")]
    [Fact]
    public Task Duplicate_Operation_Throws()
        => Given("a builder with duplicate operation", () =>
                Record.Exception(() =>
                    Facade<int, int>.Create()
                        .Operation("test", (in x) => x)
                        .Operation("test", (in x) => x + 1)
                        .Build()))
            .When("exception thrown", ex => ex)
            .Then("throws ArgumentException", ex => ex is ArgumentException)
            .And("message mentions operation name", ex => ex!.Message.Contains("test"))
            .AssertPassed();

    [Scenario("OperationIgnoreCase handles case-insensitive matching")]
    [Fact]
    public Task OperationIgnoreCase_Matches()
        => Given("a facade with case-insensitive operation", () =>
                Facade<string, string>.Create()
                    .OperationIgnoreCase("Greet", (in name) => $"Hi, {name}")
                    .Build())
            .When("execute with different casing", f => (f.Execute("greet", "Alice"), f.Execute("GREET", "Bob"), f.Execute("GrEeT", "Carol")))
            .Then("all match", r => r is { Item1: "Hi, Alice", Item2: "Hi, Bob", Item3: "Hi, Carol" })
            .AssertPassed();

    [Scenario("Build without operations or default throws")]
    [Fact]
    public Task Build_Empty_Throws()
        => Given("an empty builder", () => Record.Exception(() => Facade<int, int>.Create().Build()))
            .When("exception thrown", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .And("message mentions configuration required", ex => ex!.Message.Contains("operation") || ex.Message.Contains("default"))
            .AssertPassed();

    [Scenario("Build with only default succeeds")]
    [Fact]
    public Task Build_Default_Only_Succeeds()
        => Given("a builder with only default", () =>
                Facade<int, string>.Create()
                    .Default((in x) => $"default:{x}")
                    .Build())
            .When("execute any operation", f => f.Execute("anything", 42))
            .Then("returns default result", r => r == "default:42")
            .AssertPassed();

    [Scenario("Facade is reusable and thread-safe")]
    [Fact]
    public Task Facade_Reusable()
        => Given("a reusable facade", () =>
            {
                var callCount = new List<int>();
                var facade = Facade<int, int>.Create()
                    .Operation("increment", (in x) =>
                    {
                        callCount.Add(x);
                        return x + 1;
                    })
                    .Build();
                return (facade, callCount);
            })
            .When("execute multiple times", ctx =>
            {
                ctx.facade.Execute("increment", 1);
                ctx.facade.Execute("increment", 2);
                ctx.facade.Execute("increment", 3);
                return ctx.callCount.Count;
            })
            .Then("executed 3 times", count => count == 3)
            .AssertPassed();

    [Scenario("Complex subsystem coordination example")]
    [Fact]
    public Task Complex_Coordination()
        => Given("a multi-subsystem facade", () =>
            {
                var log = new List<string>();
                var facade = Facade<string, string>.Create()
                    .Operation("order", (in product) =>
                    {
                        log.Add("1:validate");
                        log.Add("2:reserve-inventory");
                        log.Add("3:charge-payment");
                        log.Add("4:schedule-shipping");
                        log.Add("5:send-confirmation");
                        return $"Order placed: {product}";
                    })
                    .Operation("return", (in orderId) =>
                    {
                        log.Add("1:validate-return");
                        log.Add("2:schedule-pickup");
                        log.Add("3:refund-payment");
                        log.Add("4:restock-inventory");
                        return $"Return processed: {orderId}";
                    })
                    .Build();
                return (facade, log);
            })
            .When("execute order then return", ctx =>
            {
                var orderResult = ctx.facade.Execute("order", "Laptop");
                var returnResult = ctx.facade.Execute("return", "ORD-123");
                return (orderResult, returnResult, ctx.log);
            })
            .Then("order completed", r => r.orderResult == "Order placed: Laptop")
            .And("return completed", r => r.returnResult == "Return processed: ORD-123")
            .And("all 9 subsystem calls made", r => r.log.Count == 9)
            .And("order flow correct", r => r.log[0] == "1:validate" && r.log[4] == "5:send-confirmation")
            .And("return flow correct", r => r.log[5] == "1:validate-return" && r.log[8] == "4:restock-inventory")
            .AssertPassed();
}