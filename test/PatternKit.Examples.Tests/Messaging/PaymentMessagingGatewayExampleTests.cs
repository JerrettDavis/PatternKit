using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class PaymentMessagingGatewayExampleTests
{
    [Scenario("FluentMessagingGateway AuthorizesPayment")]
    [Fact]
    public void FluentMessagingGateway_AuthorizesPayment()
    {
        var summary = PaymentMessagingGatewayExampleRunner.RunFluent(new("ORDER-100", 42.50m));

        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.True(summary.Approved);
        ScenarioExpect.Equal("AUTH-ORDER-100", summary.AuthorizationCode);
        ScenarioExpect.Equal(1, summary.RequestCount);
    }

    [Scenario("GeneratedMessagingGateway MatchesFluentGateway")]
    [Fact]
    public void GeneratedMessagingGateway_MatchesFluentGateway()
    {
        var generated = PaymentMessagingGatewayExampleRunner.RunGeneratedStatic(new("ORDER-100", 42.50m));
        var fluent = PaymentMessagingGatewayExampleRunner.RunFluent(new("ORDER-100", 42.50m));

        ScenarioExpect.Equal(fluent.Completed, generated.Completed);
        ScenarioExpect.Equal(fluent.Approved, generated.Approved);
        ScenarioExpect.Equal(fluent.AuthorizationCode, generated.AuthorizationCode);
    }

    [Scenario("ServiceCollection ImportsMessagingGatewayExample")]
    [Fact]
    public void ServiceCollection_ImportsMessagingGatewayExample()
    {
        var services = new ServiceCollection();
        services.AddPaymentMessagingGatewayDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var service = provider.GetRequiredService<PaymentMessagingGatewayService>();

        var summary = service.Authorize(new("ORDER-100", 42.50m));

        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.True(summary.Approved);
    }

    [Scenario("AggregateServiceCollection ImportsMessagingGatewayExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsMessagingGatewayExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<PaymentMessagingGatewayExampleService>();

        var summary = example.Service.Authorize(new("ORDER-100", 42.50m));

        ScenarioExpect.True(summary.Completed);
        ScenarioExpect.True(summary.Approved);
    }
}
