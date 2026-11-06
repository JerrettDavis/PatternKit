using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.ServiceLayerDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ServiceLayerDemo;

[Feature("Customer Service Layer demo")]
public sealed partial class CustomerServiceLayerDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Customer Service Layer demo registers customers")]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Customer_Service_Layer_Demo_Registers_Customers(bool sourceGenerated)
        => Given("the customer service layer demo", () => sourceGenerated)
        .When("the selected path runs", (Func<bool, ValueTask<CustomerServiceLayerSummary>>)(async generated =>
            generated
                ? await CustomerServiceLayerDemo.RunGeneratedAsync()
                : await CustomerServiceLayerDemo.RunFluentAsync()))
        .Then("the customer is registered", summary =>
        {
            ScenarioExpect.True(summary.Registered);
            ScenarioExpect.False(string.IsNullOrWhiteSpace(summary.CustomerId));
            ScenarioExpect.Equal(1, summary.RepositoryCount);
        })
        .AssertPassed();

    [Scenario("Customer Service Layer demo is importable through IServiceCollection")]
    [Fact]
    public Task Customer_Service_Layer_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service provider with the customer service layer demo", () =>
        {
            var services = new ServiceCollection();
            services.AddCustomerServiceLayerDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("a scoped workflow registers a customer", (Func<ServiceProvider, ValueTask<CustomerServiceLayerSummary>>)(async provider =>
        {
            using (provider)
            using (var scope = provider.CreateScope())
            {
                var workflow = scope.ServiceProvider.GetRequiredService<CustomerServiceLayerWorkflow>();
                return await workflow.RegisterAsync(new RegisterCustomerRequest("customer-300", "buyer3@example.com", "retail"));
            }
        }))
        .Then("the operation succeeds", summary =>
        {
            ScenarioExpect.True(summary.Registered);
            ScenarioExpect.Equal("customer-300", summary.CustomerId);
            ScenarioExpect.Equal(1, summary.RepositoryCount);
        })
        .AssertPassed();
}
