using Microsoft.Extensions.DependencyInjection;
using PatternKit.Behavioral.NullObject;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.NullObjectDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.NullObjectDemo;

[Feature("Null Object customer notification demo")]
public sealed class CustomerNotificationNullObjectDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generated null notification channel suppresses optional delivery")]
    [Fact]
    public Task Generated_Null_Notification_Channel_Suppresses_Optional_Delivery()
        => Given("a workflow using the generated null channel", () =>
                new CustomerNotificationWorkflow(NullCustomerNotificationChannel.Instance))
            .When("sending a noncritical notification", workflow =>
                workflow.Notify(new CustomerNotification("C-100", "Statement ready", "Your statement is available.")))
            .Then("the workflow receives a deterministic suppressed result", result =>
            {
                ScenarioExpect.Equal(string.Empty, result.Channel);
                ScenarioExpect.Equal("suppressed", result.Status);
                ScenarioExpect.False(result.Delivered);
            })
            .AssertPassed();

    [Scenario("Customer notification workflow validates notification input")]
    [Fact]
    public Task Customer_Notification_Workflow_Validates_Notification_Input()
        => Given("a workflow using the generated null channel", () =>
                new CustomerNotificationWorkflow(NullCustomerNotificationChannel.Instance))
            .When("sending a missing notification", workflow =>
                ScenarioExpect.Throws<ArgumentNullException>(() => workflow.Notify(null!)))
            .Then("the workflow reports the invalid notification", exception =>
                ScenarioExpect.Equal("notification", exception.ParamName))
            .AssertPassed();

    [Scenario("Null Object demo is importable through IServiceCollection")]
    [Fact]
    public Task Null_Object_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service collection with PatternKit examples", () =>
            {
                var services = new ServiceCollection();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving the null object example", provider =>
            {
                using (provider)
                {
                    var example = provider.GetRequiredService<CustomerNotificationNullObjectExample>();
                    var fallback = provider.GetRequiredService<NullObject<ICustomerNotificationChannel>>();
                    var result = example.Workflow.Notify(new CustomerNotification("C-101", "Fallback", "No channel configured."));
                    return new { example, fallback, result };
                }
            })
            .Then("the example exposes both fluent and generated paths", ctx =>
            {
                ScenarioExpect.Same(ctx.fallback.Instance, ctx.example.Channel);
                ScenarioExpect.Equal("suppressed", ctx.result.Status);
                ScenarioExpect.False(ctx.result.Delivered);
            })
            .AssertPassed();
}
