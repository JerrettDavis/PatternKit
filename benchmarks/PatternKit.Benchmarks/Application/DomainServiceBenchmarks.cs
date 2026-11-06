using BenchmarkDotNet.Attributes;
using PatternKit.Application.DomainServices;
using PatternKit.Examples.DomainServiceDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "DomainService")]
public class DomainServiceBenchmarks
{
    private static readonly ShippingDomainServiceDemo.ShippingRequest Request =
        ShippingDomainServiceDemo.CreateHighValueRequest();

    private readonly DomainServiceRegistry<ShippingDomainServiceDemo.ShippingRequest, ShippingDomainServiceDemo.ShippingDecision> _fluent =
        ShippingDomainServiceDemo.CreateFluentRegistry();

    private readonly DomainServiceRegistry<ShippingDomainServiceDemo.ShippingRequest, ShippingDomainServiceDemo.ShippingDecision> _generated =
        ShippingDomainServiceDemo.CreateGeneratedRegistry();

    [Benchmark(Baseline = true, Description = "Fluent: create domain service registry")]
    [BenchmarkCategory("Fluent", "Construction")]
    public DomainServiceRegistry<ShippingDomainServiceDemo.ShippingRequest, ShippingDomainServiceDemo.ShippingDecision> Fluent_CreateRegistry()
        => ShippingDomainServiceDemo.CreateFluentRegistry();

    [Benchmark(Description = "Generated: create domain service registry")]
    [BenchmarkCategory("Generated", "Construction")]
    public DomainServiceRegistry<ShippingDomainServiceDemo.ShippingRequest, ShippingDomainServiceDemo.ShippingDecision> Generated_CreateRegistry()
        => ShippingDomainServiceDemo.CreateGeneratedRegistry();

    [Benchmark(Description = "Fluent: select shipping decision")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ShippingDomainServiceDemo.ShippingDecision Fluent_SelectBest()
        => ShippingDomainServiceDemo.SelectBest(Request, _fluent);

    [Benchmark(Description = "Generated: select shipping decision")]
    [BenchmarkCategory("Generated", "Execution")]
    public ShippingDomainServiceDemo.ShippingDecision Generated_SelectBest()
        => ShippingDomainServiceDemo.SelectBest(Request, _generated);
}
