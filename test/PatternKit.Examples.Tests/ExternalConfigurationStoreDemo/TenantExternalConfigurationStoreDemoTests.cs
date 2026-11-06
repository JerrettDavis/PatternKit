using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ExternalConfigurationStoreDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.ExternalConfigurationStoreDemo;

public sealed class TenantExternalConfigurationStoreDemoTests
{
    [Scenario("FluentStore LoadsTenantConfiguration")]
    [Fact]
    public async Task FluentStore_LoadsTenantConfiguration()
    {
        var summary = await TenantExternalConfigurationStoreDemoRunner.RunFluentAsync();

        ScenarioExpect.True(summary.Loaded);
        ScenarioExpect.Equal("v1", summary.Version);
        ScenarioExpect.True(summary.NewCheckoutEnabled);
    }

    [Scenario("GeneratedStore MatchesFluentValidation")]
    [Fact]
    public async Task GeneratedStore_MatchesFluentValidation()
    {
        var provider = new TenantConfigurationProvider();
        TenantConfigurationProviderRegistry.Provider = provider;

        var result = await GeneratedTenantExternalConfigurationStore.Create().GetAsync();

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("tenant-a", result.Snapshot.Settings.TenantId);
    }

    [Scenario("ServiceCollection ImportsExternalConfigurationStoreExample")]
    [Fact]
    public async Task ServiceCollection_ImportsExternalConfigurationStoreExample()
    {
        var services = new ServiceCollection();
        services.AddTenantExternalConfigurationStoreDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<TenantExternalConfigurationStoreDemoRunner>();

        var summary = await runner.RunGeneratedAsync();

        ScenarioExpect.True(summary.Loaded);
        ScenarioExpect.Equal("v1", summary.Version);
    }

    [Scenario("AggregateServiceCollection ImportsExternalConfigurationStoreExample")]
    [Fact]
    public async Task AggregateServiceCollection_ImportsExternalConfigurationStoreExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<TenantExternalConfigurationStoreExample>();

        var summary = await example.Service.LoadAsync();

        ScenarioExpect.True(summary.Loaded);
        ScenarioExpect.True(summary.NewCheckoutEnabled);
    }
}
