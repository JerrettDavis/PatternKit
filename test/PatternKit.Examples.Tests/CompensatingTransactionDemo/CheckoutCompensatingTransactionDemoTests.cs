using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.CompensatingTransactions;
using PatternKit.Examples.CompensatingTransactionDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.CompensatingTransactionDemo;

[Feature("Checkout compensating transaction example")]
public sealed class CheckoutCompensatingTransactionDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated transactions compensate failed checkout")]
    [Fact]
    public Task Fluent_And_Generated_Transactions_Compensate_Failed_Checkout()
        => Given("fluent and generated checkout transactions", (Func<ValueTask<Summaries>>)(async () => new Summaries(
            await CheckoutCompensatingTransactionDemo.RunFluentAsync(),
            await CheckoutCompensatingTransactionDemo.RunGeneratedAsync())))
        .Then("both paths compensate completed work", summaries =>
        {
            ScenarioExpect.Equal(CompensatingTransactionStatus.Compensated, summaries.Fluent.Status);
            ScenarioExpect.Equal(CompensatingTransactionStatus.Compensated, summaries.Generated.Status);
            ScenarioExpect.Equal(
                ["inventory-reserved", "payment-authorized", "payment-voided", "inventory-released"],
                summaries.Fluent.Log);
            ScenarioExpect.Equal(summaries.Fluent.Log, summaries.Generated.Log);
        })
        .AssertPassed();

    [Scenario("Checkout transaction completes when all resources are available")]
    [Fact]
    public Task Checkout_Transaction_Completes_When_All_Resources_Are_Available()
        => Given("a checkout transaction with shipment capacity", () => CheckoutCompensatingTransactionDemo.RunFluentAsync(shipmentAvailable: true))
            .Then("the transaction completes without compensation", summary =>
            {
                ScenarioExpect.Equal(CompensatingTransactionStatus.Completed, summary.Status);
                ScenarioExpect.Equal(["inventory-reserved", "payment-authorized", "shipment-created"], summary.Log);
                ScenarioExpect.True(summary.History.All(static kind => kind == CompensatingTransactionRecordKind.Completed));
            })
            .AssertPassed();

    [Scenario("Compensating transaction example is importable through IServiceCollection")]
    [Fact]
    public Task Compensating_Transaction_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection importing the checkout compensating transaction example", () =>
            {
                var services = new ServiceCollection();
                services.AddCheckoutCompensatingTransactionDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
        .When("an importing application resolves and runs the workflow", provider =>
        {
            using (provider)
                return provider.GetRequiredService<CheckoutCompensatingTransactionWorkflow>()
                    .RunAsync(new CheckoutCompensatingTransactionRequest("order-1001", ShipmentAvailable: true))
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
        })
        .Then("the workflow completes", summary =>
            ScenarioExpect.Equal(CompensatingTransactionStatus.Completed, summary.Status))
        .AssertPassed();

    private sealed record Summaries(
        CheckoutCompensatingTransactionSummary Fluent,
        CheckoutCompensatingTransactionSummary Generated);
}
