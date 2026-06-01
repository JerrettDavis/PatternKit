using BenchmarkDotNet.Attributes;
using PatternKit.Examples.BackpressureDemo;
using PatternKit.Messaging.Reliability.Backpressure;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("MessagingReliability", "Backpressure")]
public class BackpressureBenchmarks
{
    private static readonly CheckoutWork Work = new("ORDER-100", 42m);

    [Benchmark(Baseline = true, Description = "Fluent: create backpressure policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public BackpressurePolicy<CheckoutAdmission> Fluent_CreatePolicy()
        => CheckoutBackpressurePolicies.CreateFluentPolicy();

    [Benchmark(Description = "Generated: create backpressure policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public BackpressurePolicy<CheckoutAdmission> Generated_CreatePolicy()
        => GeneratedCheckoutBackpressurePolicy.CreateGeneratedPolicy();

    [Benchmark(Description = "Fluent: submit checkout work")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<CheckoutAdmission> Fluent_Submit()
    {
        var service = new CheckoutBackpressureService(
            new ScriptedCheckoutProcessor(new CheckoutAdmission("", true, "accepted")),
            CheckoutBackpressurePolicies.CreateFluentPolicy());

        return service.SubmitAsync(Work);
    }

    [Benchmark(Description = "Generated: submit checkout work")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<CheckoutAdmission> Generated_Submit()
    {
        var service = new CheckoutBackpressureService(
            new ScriptedCheckoutProcessor(new CheckoutAdmission("", true, "accepted")),
            GeneratedCheckoutBackpressurePolicy.CreateGeneratedPolicy());

        return service.SubmitAsync(Work);
    }
}
