using PatternKit.Application.ServiceLayer;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.ServiceLayer;

[Feature("Service Layer")]
public sealed partial class ServiceLayerTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Service Layer completes a valid application operation")]
    [Fact]
    public Task Service_Layer_Completes_A_Valid_Application_Operation()
        => Given("a service operation", () => ServiceLayerOperation<RegisterCustomer, CustomerReceipt>.Create("register-customer")
            .Require("email", "Email is required.", static request => !string.IsNullOrWhiteSpace(request.Email))
            .Handle(static (request, _) => new ValueTask<CustomerReceipt>(new CustomerReceipt(request.Email)))
            .Build())
        .When("a valid request is executed", (Func<IServiceOperation<RegisterCustomer, CustomerReceipt>, ValueTask<ServiceLayerResult<CustomerReceipt>>>)(async operation =>
            await operation.ExecuteAsync(new RegisterCustomer("buyer@example.com"))))
        .Then("the operation returns a completed result", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal(ServiceLayerStatus.Completed, result.Status);
            ScenarioExpect.Equal("buyer@example.com", result.Response!.Email);
        })
        .AssertPassed();

    [Scenario("Service Layer rejects failed preconditions before the handler runs")]
    [Fact]
    public Task Service_Layer_Rejects_Failed_Preconditions_Before_The_Handler_Runs()
        => Given("a service operation with a rule", () =>
        {
            var handled = false;
            var operation = ServiceLayerOperation<RegisterCustomer, CustomerReceipt>.Create("register-customer")
                .Require("email", "Email is required.", static request => !string.IsNullOrWhiteSpace(request.Email))
                .Handle((request, _) =>
                {
                    handled = true;
                    return new ValueTask<CustomerReceipt>(new CustomerReceipt(request.Email));
                })
                .Build();
            return new RejectionContext(operation, () => handled);
        })
        .When("an invalid request is executed", (Func<RejectionContext, ValueTask<RejectedRegistration>>)(async ctx =>
            new RejectedRegistration(await ctx.Operation.ExecuteAsync(new RegisterCustomer("")), ctx.WasHandled)))
        .Then("the handler is skipped", ctx =>
        {
            ScenarioExpect.Equal(ServiceLayerStatus.Rejected, ctx.Result.Status);
            ScenarioExpect.False(ctx.Result.Succeeded);
            ScenarioExpect.Equal("email", ctx.Result.Code);
            ScenarioExpect.False(ctx.WasHandled());
        })
        .AssertPassed();

    [Scenario("Service Layer reports handler failures")]
    [Fact]
    public Task Service_Layer_Reports_Handler_Failures()
        => Given("a service operation with a failing handler", () => ServiceLayerOperation<RegisterCustomer, CustomerReceipt>.Create("register-customer")
            .Handle(static (_, _) => throw new InvalidOperationException("crm unavailable"))
            .Build())
        .When("the request is executed", (Func<IServiceOperation<RegisterCustomer, CustomerReceipt>, ValueTask<ServiceLayerResult<CustomerReceipt>>>)(async operation =>
            await operation.ExecuteAsync(new RegisterCustomer("buyer@example.com"))))
        .Then("the failure is returned", result =>
        {
            ScenarioExpect.Equal(ServiceLayerStatus.Failed, result.Status);
            ScenarioExpect.False(result.Succeeded);
            ScenarioExpect.IsType<InvalidOperationException>(result.Exception);
        })
        .AssertPassed();

    [Scenario("Service Layer validates required configuration")]
    [Fact]
    public Task Service_Layer_Validates_Required_Configuration()
        => Given("service layer builders", () => true)
        .Then("invalid arguments are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => ServiceLayerOperation<RegisterCustomer, CustomerReceipt>.Create(""));
            ScenarioExpect.Throws<ArgumentNullException>(() => ServiceLayerOperation<RegisterCustomer, CustomerReceipt>.Create("register").Require("email", "Email is required.", null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => ServiceLayerOperation<RegisterCustomer, CustomerReceipt>.Create("register").Handle(null!));
            ScenarioExpect.Throws<InvalidOperationException>(() => ServiceLayerOperation<RegisterCustomer, CustomerReceipt>.Create("register").Build());
            ScenarioExpect.Throws<ArgumentNullException>(() => ServiceLayerOperation<RegisterCustomer, CustomerReceipt>.Create("register")
                .Handle(static (request, _) => new ValueTask<CustomerReceipt>(new CustomerReceipt(request.Email)))
                .Build()
                .ExecuteAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentException>(() => new ServiceLayerRule<RegisterCustomer>("", "message", static _ => true));
            ScenarioExpect.Throws<ArgumentException>(() => new ServiceLayerRule<RegisterCustomer>("code", "", static _ => true));
            ScenarioExpect.Throws<ArgumentNullException>(() => new ServiceLayerRule<RegisterCustomer>("code", "message", null!));
            ScenarioExpect.Throws<ArgumentException>(() => ServiceLayerResult<CustomerReceipt>.Rejected("", "message"));
            ScenarioExpect.Throws<ArgumentException>(() => ServiceLayerResult<CustomerReceipt>.Rejected("code", ""));
            ScenarioExpect.Throws<ArgumentNullException>(() => ServiceLayerResult<CustomerReceipt>.Failed(null!));
        })
        .AssertPassed();

    private sealed record RegisterCustomer(string Email);

    private sealed record CustomerReceipt(string Email);

    private sealed record RejectionContext(IServiceOperation<RegisterCustomer, CustomerReceipt> Operation, Func<bool> WasHandled);

    private sealed record RejectedRegistration(ServiceLayerResult<CustomerReceipt> Result, Func<bool> WasHandled);
}
