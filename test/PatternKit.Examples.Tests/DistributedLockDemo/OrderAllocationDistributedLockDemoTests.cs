using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.DistributedLocks;
using PatternKit.Examples.DistributedLockDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.DistributedLockDemo;

[Feature("Order allocation distributed lock demo")]
public sealed class OrderAllocationDistributedLockDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated distributed lock paths protect order allocation")]
    [Fact]
    public Task Fluent_And_Generated_Distributed_Lock_Paths_Protect_Order_Allocation()
        => Given("fluent and generated order allocation lock workflows", () => new
        {
            Fluent = OrderAllocationDistributedLockDemoRunner.RunFluent(),
            Generated = new OrderAllocationLockWorkflow(GeneratedOrderAllocationDistributedLock.Create())
                .Allocate(new("ORDER-200", "allocator-b", 2))
        })
        .Then("both workflows acquire block contenders and release the order lock", result =>
        {
            ScenarioExpect.True(result.Fluent.Acquired);
            ScenarioExpect.True(result.Fluent.BlockedWhileActive);
            ScenarioExpect.True(result.Fluent.Released);
            ScenarioExpect.True(result.Generated.Acquired);
            ScenarioExpect.True(result.Generated.BlockedWhileActive);
            ScenarioExpect.True(result.Generated.Released);
        })
        .AssertPassed();

    [Scenario("Distributed lock example imports through IServiceCollection")]
    [Fact]
    public Task Distributed_Lock_Example_Imports_Through_IServiceCollection()
        => Given("a service collection with the order allocation distributed lock demo", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderAllocationDistributedLockDemo();
            return services.BuildServiceProvider();
        })
        .When("the runner is resolved and executed", provider => new
        {
            Lock = provider.GetRequiredService<DistributedLock<string>>(),
            Summary = provider.GetRequiredService<OrderAllocationDistributedLockDemoRunner>().RunGenerated()
        })
        .Then("the container owns the generated lock and workflow", result =>
        {
            ScenarioExpect.Equal("order-allocation-lock", result.Lock.Name);
            ScenarioExpect.True(result.Summary.Acquired);
            ScenarioExpect.True(result.Summary.Released);
        })
        .AssertPassed();

    [Scenario("Order allocation workflow reports blocked resources")]
    [Fact]
    public Task Order_Allocation_Workflow_Reports_Blocked_Resources()
        => Given("an order allocation workflow with an active lease", () =>
        {
            var mutex = GeneratedOrderAllocationDistributedLock.Create();
            mutex.TryAcquire("ORDER-300", "allocator-a");
            return new OrderAllocationLockWorkflow(mutex);
        })
        .When("another worker attempts to allocate the same order", workflow => new
        {
            Workflow = workflow,
            Summary = workflow.Allocate(new("ORDER-300", "allocator-b", 1))
        })
        .Then("the workflow reports the allocation as blocked", summary =>
        {
            ScenarioExpect.False(summary.Summary.Acquired);
            ScenarioExpect.False(summary.Summary.Released);
            ScenarioExpect.True(summary.Summary.BlockedWhileActive);
        })
        .And("null requests are rejected", result =>
            ScenarioExpect.Throws<ArgumentNullException>(() => result.Workflow.Allocate(null!)))
        .AssertPassed();
}
