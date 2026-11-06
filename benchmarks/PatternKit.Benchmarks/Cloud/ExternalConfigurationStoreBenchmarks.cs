using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.ExternalConfigurationStore;
using PatternKit.Examples.ExternalConfigurationStoreDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "ExternalConfigurationStore")]
public class ExternalConfigurationStoreBenchmarks
{
    private static readonly TenantConfigurationProvider Provider = new();
    private readonly ExternalConfigurationStore<TenantFeatureSettings> _fluent =
        TenantExternalConfigurationStores.Create(Provider);
    private readonly ExternalConfigurationStore<TenantFeatureSettings> _generated =
        GeneratedTenantExternalConfigurationStore.Create();

    [GlobalSetup]
    public void Setup()
    {
        TenantConfigurationProviderRegistry.Provider = Provider;
    }

    [Benchmark(Baseline = true, Description = "Fluent: create external configuration store")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ExternalConfigurationStore<TenantFeatureSettings> Fluent_CreateStore()
        => TenantExternalConfigurationStores.Create(Provider);

    [Benchmark(Description = "Generated: create external configuration store")]
    [BenchmarkCategory("Generated", "Construction")]
    public ExternalConfigurationStore<TenantFeatureSettings> Generated_CreateStore()
        => GeneratedTenantExternalConfigurationStore.Create();

    [Benchmark(Description = "Fluent: load tenant feature settings")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<ExternalConfigurationResult<TenantFeatureSettings>> Fluent_LoadTenantSettings()
        => _fluent.GetAsync();

    [Benchmark(Description = "Generated: load tenant feature settings")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<ExternalConfigurationResult<TenantFeatureSettings>> Generated_LoadTenantSettings()
        => _generated.GetAsync();
}
