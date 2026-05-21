using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.AuditLogDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.AuditLogDemo;

[Feature("Order Audit Log demo")]
public sealed partial class OrderAuditLogDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Order Audit Log demo records order actions")]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Order_Audit_Log_Demo_Records_Order_Actions(bool sourceGenerated)
        => Given("the order audit log demo", () => sourceGenerated)
        .When("the selected path runs", (Func<bool, ValueTask<OrderAuditLogSummary>>)(async generated =>
            generated
                ? await OrderAuditLogDemo.RunGeneratedAsync()
                : await OrderAuditLogDemo.RunFluentAsync()))
        .Then("the audit log contains submitted and approved entries", summary =>
        {
            ScenarioExpect.Equal("order-audit", summary.LogName);
            ScenarioExpect.Equal(2, summary.EntryCount);
            ScenarioExpect.Equal("approved", summary.LastAction);
        })
        .AssertPassed();

    [Scenario("Order Audit Log demo is importable through IServiceCollection")]
    [Fact]
    public Task Order_Audit_Log_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service provider with the order audit log demo", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderAuditLogDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("a scoped workflow records order actions", (Func<ServiceProvider, ValueTask<OrderAuditLogSummary>>)(async provider =>
        {
            using (provider)
            using (var scope = provider.CreateScope())
            {
                var workflow = scope.ServiceProvider.GetRequiredService<OrderAuditLogWorkflow>();
                return await workflow.SubmitAndApproveAsync("order-300");
            }
        }))
        .Then("the imported audit log stores the workflow entries", summary =>
        {
            ScenarioExpect.Equal("order-300", summary.OrderId);
            ScenarioExpect.Equal(2, summary.EntryCount);
            ScenarioExpect.Equal("approved", summary.LastAction);
        })
        .AssertPassed();
}
