using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.CircuitBreaker;
using PatternKit.Examples.CircuitBreakerDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "CircuitBreaker")]
public class CircuitBreakerBenchmarks
{
    private static readonly FulfillmentResponse Accepted = new("ORDER-42", 202, "accepted");

    [Benchmark(Baseline = true, Description = "Fluent: create circuit breaker policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public CircuitBreakerPolicy<FulfillmentResponse> Fluent_CreatePolicy()
        => FulfillmentCircuitBreakerPolicies.CreateFluentPolicy();

    [Benchmark(Description = "Generated: create circuit breaker policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public CircuitBreakerPolicy<FulfillmentResponse> Generated_CreatePolicy()
        => GeneratedFulfillmentCircuitBreakerPolicy.CreateGeneratedPolicy();

    [Benchmark(Description = "Fluent: submit accepted fulfillment")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<FulfillmentSubmissionResult> Fluent_SubmitAcceptedFulfillment()
    {
        var service = new FulfillmentCircuitBreakerService(
            new ScriptedFulfillmentGateway(Accepted),
            FulfillmentCircuitBreakerPolicies.CreateFluentPolicy());

        return service.SubmitAsync("ORDER-42");
    }

    [Benchmark(Description = "Generated: submit accepted fulfillment")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<FulfillmentSubmissionResult> Generated_SubmitAcceptedFulfillment()
    {
        var service = new FulfillmentCircuitBreakerService(
            new ScriptedFulfillmentGateway(Accepted),
            GeneratedFulfillmentCircuitBreakerPolicy.CreateGeneratedPolicy());

        return service.SubmitAsync("ORDER-42");
    }
}
