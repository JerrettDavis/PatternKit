using PatternKit.Application.TransactionScript;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.TransactionScript;

[Feature("Transaction Script")]
public sealed partial class TransactionScriptTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Transaction Script validates and executes a request workflow")]
    [Fact]
    public Task Transaction_Script_Validates_And_Executes_A_Request_Workflow()
        => Given("a transaction script", () => TransactionScript<SubmitOrder, OrderReceipt>.Create("submit-order")
            .Validate(static request => request.Total <= 0m
                ? [new TransactionScriptError("total", "Total must be positive.")]
                : [])
            .Execute(static (request, _) => new ValueTask<OrderReceipt>(new OrderReceipt(request.OrderId, request.Total)))
            .Build())
        .When("a valid request is executed", (Func<ITransactionScript<SubmitOrder, OrderReceipt>, ValueTask<TransactionScriptResult<OrderReceipt>>>)(async script =>
            await script.ExecuteAsync(new SubmitOrder("order-100", 125m))))
        .Then("the response is completed", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal(TransactionScriptStatus.Completed, result.Status);
            ScenarioExpect.Equal("order-100", result.Response!.OrderId);
            ScenarioExpect.Empty(result.Errors);
        })
        .AssertPassed();

    [Scenario("Transaction Script rejects invalid requests before the handler runs")]
    [Fact]
    public Task Transaction_Script_Rejects_Invalid_Requests_Before_The_Handler_Runs()
        => Given("a transaction script with validation", () =>
        {
            var handled = false;
            var script = TransactionScript<SubmitOrder, OrderReceipt>.Create("submit-order")
                .Validate(static _ => [new TransactionScriptError("invalid", "Order is invalid.")])
                .Execute((request, _) =>
                {
                    handled = true;
                    return new ValueTask<OrderReceipt>(new OrderReceipt(request.OrderId, request.Total));
                })
                .Build();
            return new SubmitOrderScriptContext(script, () => handled);
        })
        .When("an invalid request is executed", (Func<SubmitOrderScriptContext, ValueTask<RejectedSubmitOrderResult>>)(async ctx =>
            new RejectedSubmitOrderResult(await ctx.Script.ExecuteAsync(new SubmitOrder("order-100", 0m)), ctx.WasHandled)))
        .Then("the handler is skipped", ctx =>
        {
            ScenarioExpect.Equal(TransactionScriptStatus.Rejected, ctx.Result.Status);
            ScenarioExpect.False(ctx.Result.Succeeded);
            ScenarioExpect.False(ctx.WasHandled());
            ScenarioExpect.Equal("invalid", ScenarioExpect.Single(ctx.Result.Errors).Code);
        })
        .AssertPassed();

    [Scenario("Transaction Script reports handled failures")]
    [Fact]
    public Task Transaction_Script_Reports_Handled_Failures()
        => Given("a transaction script with a failing handler", () => TransactionScript<SubmitOrder, OrderReceipt>.Create("submit-order")
            .Execute(static (_, _) => throw new InvalidOperationException("database unavailable"))
            .Build())
        .When("the request is executed", (Func<ITransactionScript<SubmitOrder, OrderReceipt>, ValueTask<TransactionScriptResult<OrderReceipt>>>)(async script =>
            await script.ExecuteAsync(new SubmitOrder("order-100", 125m))))
        .Then("the failure is returned", result =>
        {
            ScenarioExpect.Equal(TransactionScriptStatus.Failed, result.Status);
            ScenarioExpect.False(result.Succeeded);
            ScenarioExpect.IsType<InvalidOperationException>(result.Exception);
        })
        .AssertPassed();

    [Scenario("Transaction Script validates required configuration")]
    [Fact]
    public Task Transaction_Script_Validates_Required_Configuration()
        => Given("transaction script builders", () => true)
        .Then("invalid arguments are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => TransactionScript<SubmitOrder, OrderReceipt>.Create(""));
            ScenarioExpect.Throws<ArgumentNullException>(() => TransactionScript<SubmitOrder, OrderReceipt>.Create("submit").Validate(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => TransactionScript<SubmitOrder, OrderReceipt>.Create("submit").Execute(null!));
            ScenarioExpect.Throws<InvalidOperationException>(() => TransactionScript<SubmitOrder, OrderReceipt>.Create("submit").Build());
            ScenarioExpect.Throws<ArgumentNullException>(() => TransactionScript<SubmitOrder, OrderReceipt>.Create("submit")
                .Execute(static (request, _) => new ValueTask<OrderReceipt>(new OrderReceipt(request.OrderId, request.Total)))
                .Build()
                .ExecuteAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentException>(() => TransactionScriptResult<OrderReceipt>.Rejected(Array.Empty<TransactionScriptError>()));
            ScenarioExpect.Throws<ArgumentNullException>(() => TransactionScriptResult<OrderReceipt>.Rejected(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => TransactionScriptResult<OrderReceipt>.Failed(null!));
            ScenarioExpect.Throws<ArgumentException>(() => new TransactionScriptError("", "message"));
            ScenarioExpect.Throws<ArgumentException>(() => new TransactionScriptError("code", ""));
        })
        .AssertPassed();

    [Scenario("Transaction Script treats null validator output as no errors")]
    [Fact]
    public Task Transaction_Script_Treats_Null_Validator_Output_As_No_Errors()
        => Given("a transaction script with a null-returning validator", () => TransactionScript<SubmitOrder, OrderReceipt>.Create("submit")
            .Validate(static _ => null!)
            .Execute(static (request, _) => new ValueTask<OrderReceipt>(new OrderReceipt(request.OrderId, request.Total)))
            .Build())
        .When("the request is executed", (Func<ITransactionScript<SubmitOrder, OrderReceipt>, ValueTask<TransactionScriptResult<OrderReceipt>>>)(async script =>
            await script.ExecuteAsync(new SubmitOrder("order-100", 125m))))
        .Then("the handler still completes", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal(TransactionScriptStatus.Completed, result.Status);
        })
        .AssertPassed();

    private sealed record SubmitOrder(string OrderId, decimal Total);

    private sealed record OrderReceipt(string OrderId, decimal Total);

    private sealed record SubmitOrderScriptContext(ITransactionScript<SubmitOrder, OrderReceipt> Script, Func<bool> WasHandled);

    private sealed record RejectedSubmitOrderResult(TransactionScriptResult<OrderReceipt> Result, Func<bool> WasHandled);
}
