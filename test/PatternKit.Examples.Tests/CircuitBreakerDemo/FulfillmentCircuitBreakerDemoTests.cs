using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.CircuitBreaker;
using PatternKit.Examples.CircuitBreakerDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.CircuitBreakerDemo;

[Feature("Fulfillment circuit breaker demo")]
public sealed class FulfillmentCircuitBreakerDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated circuit breaker policies isolate fulfillment outages")]
    [Fact]
    public Task Fluent_And_Generated_Circuit_Breaker_Policies_Isolate_Fulfillment_Outages()
        => Given("an unstable fulfillment gateway", static () => new
        {
            FluentGateway = new ScriptedFulfillmentGateway(
                    new FulfillmentResponse("ORDER-42", 503, "down"),
                    new FulfillmentResponse("ORDER-42", 503, "down"),
                    new FulfillmentResponse("ORDER-42", 202, "accepted")),
            GeneratedGateway = new ScriptedFulfillmentGateway(
                    new FulfillmentResponse("ORDER-42", 503, "down"),
                    new FulfillmentResponse("ORDER-42", 503, "down"),
                    new FulfillmentResponse("ORDER-42", 202, "accepted"))
        })
            .When("submitting orders through both policy paths", ctx =>
            {
                var fluent = new FulfillmentCircuitBreakerService(ctx.FluentGateway, FulfillmentCircuitBreakerPolicies.CreateFluentPolicy());
                var generated = new FulfillmentCircuitBreakerService(ctx.GeneratedGateway, GeneratedFulfillmentCircuitBreakerPolicy.CreateGeneratedPolicy());

                return new
                {
                    FluentFirst = fluent.SubmitAsync("ORDER-42").GetAwaiter().GetResult(),
                    FluentSecond = fluent.SubmitAsync("ORDER-42").GetAwaiter().GetResult(),
                    FluentRejected = fluent.SubmitAsync("ORDER-42").GetAwaiter().GetResult(),
                    GeneratedFirst = generated.SubmitAsync("ORDER-42").GetAwaiter().GetResult(),
                    GeneratedSecond = generated.SubmitAsync("ORDER-42").GetAwaiter().GetResult(),
                    GeneratedRejected = generated.SubmitAsync("ORDER-42").GetAwaiter().GetResult(),
                    ctx.FluentGateway.Calls,
                    GeneratedCalls = ctx.GeneratedGateway.Calls
                };
            })
            .Then("both paths open the breaker and reject the third dependency call", result =>
            {
                ScenarioExpect.False(result.FluentFirst.Accepted);
                ScenarioExpect.Equal(CircuitBreakerState.Closed, result.FluentFirst.State);
                ScenarioExpect.Equal(CircuitBreakerState.Open, result.FluentSecond.State);
                ScenarioExpect.True(result.FluentRejected.Rejected);
                ScenarioExpect.Equal(CircuitBreakerState.Open, result.GeneratedSecond.State);
                ScenarioExpect.True(result.GeneratedRejected.Rejected);
            })
            .And("the gateway is not called once the circuit is open", result =>
            {
                ScenarioExpect.Equal(2, result.Calls);
                ScenarioExpect.Equal(2, result.GeneratedCalls);
            })
            .AssertPassed();

    [Scenario("Fulfillment circuit breaker demo registers with IServiceCollection")]
    [Fact]
    public Task Fulfillment_Circuit_Breaker_Demo_Registers_With_IServiceCollection()
        => Given("a standard service collection", static () =>
            {
                var services = new ServiceCollection();
                services.AddFulfillmentCircuitBreakerDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and using the fulfillment circuit breaker service", provider =>
            {
                using (provider)
                {
                    var service = provider.GetRequiredService<FulfillmentCircuitBreakerService>();
                    _ = service.SubmitAsync("ORDER-42").GetAwaiter().GetResult();
                    var opened = service.SubmitAsync("ORDER-42").GetAwaiter().GetResult();
                    var rejected = service.SubmitAsync("ORDER-42").GetAwaiter().GetResult();
                    return new { Opened = opened, Rejected = rejected };
                }
            })
            .Then("the registered service uses the generated circuit breaker policy", result =>
            {
                ScenarioExpect.Equal(CircuitBreakerState.Open, result.Opened.State);
                ScenarioExpect.True(result.Rejected.Rejected);
            })
            .AssertPassed();
}
