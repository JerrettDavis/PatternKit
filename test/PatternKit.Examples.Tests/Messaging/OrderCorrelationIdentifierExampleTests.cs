using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Correlation;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Order Correlation Identifier example")]
public sealed partial class OrderCorrelationIdentifierExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent Example Correlates Request And Reply")]
    [Fact]
    public Task Fluent_Example_Correlates_Request_And_Reply()
        => Given("the fluent order correlation runner", () => new OrderCorrelationService(OrderCorrelationIdentifiers.Create().Build()))
            .When("an order is accepted", service => service.Accept(new("ord-100", "cust-7", 42.50m)))
            .Then("request and reply share the order correlation", summary =>
            {
                ScenarioExpect.Equal("order:ord-100", summary.RequestCorrelationId);
                ScenarioExpect.Equal("order:ord-100", summary.ReplyCorrelationId);
                ScenarioExpect.Equal("customer:cust-7", summary.CustomHeaderCorrelationId);
            })
            .AssertPassed();

    [Scenario("Generated Example Builds Correlator")]
    [Fact]
    public Task Generated_Example_Builds_Correlator()
        => Given("the generated order correlation builder", GeneratedOrderCorrelation.Create)
            .When("the generated correlator is used", builder => builder
                .Select(static (message, _) => "customer:" + message.Payload.CustomerId)
                .Build()
                .Ensure(PatternKit.Messaging.Message<CorrelatedOrder>.Create(new("ord-100", "cust-7", 42.50m))))
            .Then("the configured custom header carries the selected correlation", message =>
                ScenarioExpect.Equal("customer:cust-7", message.Headers.GetString("X-Correlation")))
            .AssertPassed();

    [Scenario("ServiceCollection Registers Correlation Example")]
    [Fact]
    public Task ServiceCollection_Registers_Correlation_Example()
        => Given("a service collection with the correlation identifier demo", () =>
            {
                var services = new ServiceCollection();
                services.AddOrderCorrelationIdentifierDemo();
                return services.BuildServiceProvider();
            })
            .When("the example services are resolved", provider => new
            {
                Correlation = provider.GetRequiredService<CorrelationIdentifier<CorrelatedOrder>>(),
                Service = provider.GetRequiredService<OrderCorrelationService>(),
                Runner = provider.GetRequiredService<OrderCorrelationIdentifierExampleRunner>()
            })
            .Then("the registered services run the production example", resolved =>
            {
                ScenarioExpect.Equal("correlation-id", resolved.Correlation.HeaderName);
                ScenarioExpect.Equal("order:ord-100", resolved.Service.Accept(new("ord-100", "cust-7", 42.50m)).RequestCorrelationId);
                ScenarioExpect.Equal("order:ord-100", resolved.Runner.RunFluent().RequestCorrelationId);
                ScenarioExpect.Equal("customer:cust-7", resolved.Runner.RunGenerated().RequestCorrelationId);
            })
            .AssertPassed();
}
