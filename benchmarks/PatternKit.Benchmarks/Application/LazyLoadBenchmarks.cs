using BenchmarkDotNet.Attributes;
using PatternKit.Application.LazyLoading;
using PatternKit.Examples.LazyLoadDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "LazyLoad")]
public class LazyLoadBenchmarks
{
    private static readonly ICustomerProfileStore Store = new InMemoryCustomerProfileStore(new CustomerProfile(Guid.Empty, "Ada Lovelace", "Gold"));

    [Benchmark(Baseline = true, Description = "Fluent: create lazy load")]
    [BenchmarkCategory("Fluent", "Construction")]
    public LazyLoad<CustomerProfile> Fluent_Create()
        => CustomerProfileLazyLoadPolicies.CreateFluent(Store);

    [Benchmark(Description = "Generated: create lazy load")]
    [BenchmarkCategory("Generated", "Construction")]
    public LazyLoad<CustomerProfile> Generated_Create()
    {
        GeneratedCustomerProfileLazyLoad.UseStore(Store);
        return GeneratedCustomerProfileLazyLoad.CreateGenerated();
    }

    [Benchmark(Description = "Fluent: resolve lazy value")]
    [BenchmarkCategory("Fluent", "Execution")]
    public async ValueTask<string> Fluent_Resolve()
    {
        var service = new CustomerProfileLazyLoadService(CustomerProfileLazyLoadPolicies.CreateFluent(Store));
        return await service.GetTierAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Generated: resolve lazy value")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<string> Generated_Resolve()
    {
        GeneratedCustomerProfileLazyLoad.UseStore(Store);
        var service = new CustomerProfileLazyLoadService(GeneratedCustomerProfileLazyLoad.CreateGenerated());
        return await service.GetTierAsync().ConfigureAwait(false);
    }
}
