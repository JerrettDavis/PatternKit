using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DataMapperDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.DataMapperDemo;

[Feature("Order Data Mapper example")]
public sealed partial class OrderDataMapperDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated Data Mapper paths persist and reload orders")]
    [Fact]
    public Task Fluent_And_Generated_Data_Mapper_Paths_Persist_And_Reload_Orders()
        => Given(
            "fluent and generated order Data Mappers",
            (Func<Task<OrderDataMapperRuns>>)(async () => new OrderDataMapperRuns(
                await OrderDataMapperDemo.RunFluentAsync(),
                await OrderDataMapperDemo.RunGeneratedAsync())))
            .Then("both mapping paths store rows and rehydrate domain orders", runs =>
            {
                ScenarioExpect.True(runs.Fluent.DataMapped);
                ScenarioExpect.True(runs.Fluent.DomainMapped);
                ScenarioExpect.Equal("PAID", runs.Fluent.StoredStatus);
                ScenarioExpect.Equal("customer-1", runs.Generated.LoadedCustomerId);
            })
            .AssertPassed();

    [Scenario("Data Mapper example exposes validation failures")]
    [Fact]
    public Task Data_Mapper_Example_Exposes_Validation_Failures()
        => Given("an invalid domain order", OrderDataMapperDemo.RunValidationAsync)
            .Then("the mapper returns validation errors instead of storing a row", summary =>
            {
                ScenarioExpect.False(summary.DataMapped);
                ScenarioExpect.Equal(1, summary.ValidationErrors);
            })
            .AssertPassed();

    [Scenario("Data Mapper example is importable through IServiceCollection")]
    [Fact]
    public Task Data_Mapper_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection importing the order Data Mapper example", () =>
            {
                var services = new ServiceCollection();
                services.AddOrderDataMapperDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("an importing application runs the workflow", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<OrderDataMapperWorkflow>().RunAsync().AsTask().GetAwaiter().GetResult();
            })
            .Then("the workflow maps and loads the order", summary =>
            {
                ScenarioExpect.True(summary.DataMapped);
                ScenarioExpect.True(summary.DomainMapped);
                ScenarioExpect.Equal("customer-1", summary.LoadedCustomerId);
            })
            .AssertPassed();

    private sealed record OrderDataMapperRuns(
        OrderDataMapperSummary Fluent,
        OrderDataMapperSummary Generated);
}
