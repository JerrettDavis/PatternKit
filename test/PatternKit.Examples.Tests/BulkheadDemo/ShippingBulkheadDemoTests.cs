using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.Bulkhead;
using PatternKit.Examples.BulkheadDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.BulkheadDemo;

[Feature("Shipping bulkhead demo")]
public sealed class ShippingBulkheadDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated bulkhead policies reserve shipping allocations")]
    [Fact]
    public Task Fluent_And_Generated_Bulkhead_Policies_Reserve_Shipping_Allocations()
        => Given("shipping allocators imported by an application", static () => new
            {
                FluentAllocator = new ScriptedShippingAllocator(new ShippingAllocation("ORDER-100", "ground", true)),
                GeneratedAllocator = new ScriptedShippingAllocator(new ShippingAllocation("ORDER-100", "ground", true))
            })
            .When("reserving through both policy paths", ctx => new
            {
                Fluent = new ShippingBulkheadService(ctx.FluentAllocator, ShippingBulkheadPolicies.CreateFluentPolicy())
                    .ReserveAsync("ORDER-100").GetAwaiter().GetResult(),
                Generated = new ShippingBulkheadService(ctx.GeneratedAllocator, GeneratedShippingBulkheadPolicy.CreateGeneratedPolicy())
                    .ReserveAsync("ORDER-100").GetAwaiter().GetResult()
            })
            .Then("both paths return a successful allocation", result =>
            {
                ScenarioExpect.True(result.Fluent.Succeeded);
                ScenarioExpect.True(result.Generated.Succeeded);
                ScenarioExpect.Equal("ground", result.Fluent.Carrier);
                ScenarioExpect.Equal(result.Fluent.Carrier, result.Generated.Carrier);
            })
            .AssertPassed();

    [Scenario("Generated bulkhead policy rejects overflow when no queue is configured")]
    [Fact]
    public async Task Generated_Bulkhead_Policy_Rejects_Overflow_When_No_Queue_Is_Configured()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BulkheadPolicy<ShippingAllocation>.Create("shipping-allocation")
            .WithMaxConcurrency(1)
            .WithMaxQueueLength(0)
            .Build();

        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return new ShippingAllocation("ORDER-100", "ground", true);
        });
        await entered.Task;

        var rejected = await new ShippingBulkheadService(
            new ScriptedShippingAllocator(new ShippingAllocation("ORDER-101", "air", true)),
            policy).ReserveAsync("ORDER-101");

        release.SetResult();
        _ = await first;

        ScenarioExpect.True(rejected.Rejected);
        ScenarioExpect.False(rejected.Succeeded);
    }

    [Scenario("Shipping bulkhead demo registers with IServiceCollection")]
    [Fact]
    public Task Shipping_Bulkhead_Demo_Registers_With_IServiceCollection()
        => Given("a standard service collection", static () =>
            {
                var services = new ServiceCollection();
                services.AddShippingBulkheadDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and using the shipping bulkhead service", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<ShippingBulkheadService>().ReserveAsync("ORDER-100").GetAwaiter().GetResult();
            })
            .Then("the registered service uses the generated bulkhead policy", result =>
            {
                ScenarioExpect.True(result.Succeeded);
                ScenarioExpect.True(result.Reserved);
                ScenarioExpect.Equal("ground", result.Carrier);
            })
            .AssertPassed();
}
