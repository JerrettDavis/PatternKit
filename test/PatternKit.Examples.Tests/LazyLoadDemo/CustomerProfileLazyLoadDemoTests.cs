using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.LazyLoading;
using PatternKit.Examples.LazyLoadDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.LazyLoadDemo;

public sealed class CustomerProfileLazyLoadDemoTests
{
    [Scenario("Customer profile lazy load works through fluent and generated policies")]
    [Fact]
    public async Task Customer_Profile_Lazy_Load_Works_Through_Fluent_And_Generated_Policies()
    {
        var store = new InMemoryCustomerProfileStore(new CustomerProfile(Guid.Empty, "Ada Lovelace", "Gold"));
        var fluent = CustomerProfileLazyLoadPolicies.CreateFluent(store);
        GeneratedCustomerProfileLazyLoad.UseStore(store);
        var generated = GeneratedCustomerProfileLazyLoad.CreateGenerated();

        var fluentResult = await fluent.GetAsync();
        var generatedResult = await generated.GetAsync();

        ScenarioExpect.True(fluentResult.Loaded);
        ScenarioExpect.True(generatedResult.Loaded);
        ScenarioExpect.Equal("Gold", generatedResult.Value.Tier);
    }

    [Scenario("Customer profile lazy load is importable through IServiceCollection")]
    [Fact]
    public async Task Customer_Profile_Lazy_Load_Is_Importable_Through_ServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddCustomerProfileLazyLoadDemo()
            .BuildServiceProvider();

        var lazy = provider.GetRequiredService<LazyLoad<CustomerProfile>>();
        var service = provider.GetRequiredService<CustomerProfileLazyLoadService>();

        var first = await service.GetTierAsync();
        var second = await service.GetTierAsync();

        ScenarioExpect.Equal("customer-profile", lazy.Name);
        ScenarioExpect.Equal("Gold", first);
        ScenarioExpect.Equal("Gold", second);
        ScenarioExpect.True(lazy.IsLoaded);
    }

    [Scenario("Generated customer profile lazy load reports missing store")]
    [Fact]
    public async Task Generated_Customer_Profile_Lazy_Load_Reports_Missing_Store()
    {
        var store = new InMemoryCustomerProfileStore(new CustomerProfile(Guid.Empty, "Ada Lovelace", "Gold"));
        GeneratedCustomerProfileLazyLoad.UseStore(null!);
        var generated = GeneratedCustomerProfileLazyLoad.CreateGenerated();

        try
        {
            await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() => generated.GetAsync().AsTask());
        }
        finally
        {
            GeneratedCustomerProfileLazyLoad.UseStore(store);
        }
    }
}
