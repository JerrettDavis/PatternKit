using BenchmarkDotNet.Attributes;
using PatternKit.Application.Repository;
using PatternKit.Application.ServiceLayer;
using PatternKit.Examples.ServiceLayerDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "ServiceLayer")]
public class ServiceLayerBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create service layer operation")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ServiceLayerOperation<RegisterCustomerRequest, CustomerRegistrationReceipt> Fluent_CreateOperation()
        => CustomerServiceLayerPolicies.CreateFluentOperation(
            InMemoryRepository<RegisteredCustomer, string>.Create(static customer => customer.CustomerId).Build());

    [Benchmark(Description = "Generated: create service layer operation")]
    [BenchmarkCategory("Generated", "Construction")]
    public IServiceOperation<RegisterCustomerRequest, CustomerRegistrationReceipt> Generated_CreateOperation()
        => GeneratedCustomerServiceLayer.CreateOperation();

    [Benchmark(Description = "Fluent: register customer")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<CustomerServiceLayerSummary> Fluent_RegisterCustomer()
        => CustomerServiceLayerDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: register customer")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<CustomerServiceLayerSummary> Generated_RegisterCustomer()
        => CustomerServiceLayerDemo.RunGeneratedAsync();
}
