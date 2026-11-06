using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class OrderMessageFilterExampleTests
{
    [Scenario("FluentMessageFilter AcceptsTrustedOrLowValueOrders")]
    [Theory]
    [InlineData("trusted", 250, true, true, "trusted-customer")]
    [InlineData("guest", 75, true, true, "verified-low-value")]
    [InlineData("guest", 250, true, false, null)]
    [InlineData("trusted", 250, false, false, null)]
    public void FluentMessageFilter_AcceptsTrustedOrLowValueOrders(
        string tier,
        decimal total,
        bool paymentVerified,
        bool expectedAccepted,
        string? expectedRule)
    {
        var summary = OrderMessageFilterExampleRunner.RunFluent(new("order-1", tier, total, paymentVerified));

        ScenarioExpect.Equal(expectedAccepted, summary.Accepted);
        ScenarioExpect.Equal(expectedRule, summary.RuleName);
        ScenarioExpect.Equal(expectedAccepted ? null : "Order requires fraud review before fulfillment.", summary.RejectionReason);
    }

    [Scenario("GeneratedMessageFilter MatchesFluentFilterBehavior")]
    [Fact]
    public void GeneratedMessageFilter_MatchesFluentFilterBehavior()
    {
        var command = new OrderMessageFilterCommand("order-1", "guest", 75m, true);

        var fluent = OrderMessageFilterExampleRunner.RunFluent(command);
        var generated = GeneratedOrderMessageFilter.Create().Filter(Message<OrderMessageFilterCommand>.Create(command));

        ScenarioExpect.True(generated.Accepted);
        ScenarioExpect.Equal(fluent.RuleName, generated.RuleName);
    }

    [Scenario("ServiceCollection ImportsMessageFilterExample")]
    [Fact]
    public void ServiceCollection_ImportsMessageFilterExample()
    {
        var services = new ServiceCollection();
        services.AddOrderMessageFilterDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var filter = provider.GetRequiredService<MessageFilter<OrderMessageFilterCommand>>();
        var runner = provider.GetRequiredService<OrderMessageFilterExampleRunner>();

        var direct = filter.Filter(Message<OrderMessageFilterCommand>.Create(new("order-1", "trusted", 250m, true)));
        var summary = runner.RunGenerated(new("order-2", "guest", 250m, true));

        ScenarioExpect.True(direct.Accepted);
        ScenarioExpect.False(summary.Accepted);
        ScenarioExpect.Equal("Order requires fraud review before fulfillment.", summary.RejectionReason);
    }

    [Scenario("AggregateServiceCollection ImportsMessageFilterExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsMessageFilterExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderMessageFilterExampleService>();

        var summary = example.Service.Screen(new("order-1", "trusted", 250m, true));

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal("trusted-customer", summary.RuleName);
    }
}
