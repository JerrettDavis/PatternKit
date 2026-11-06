using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ErpChannelAdapterExampleTests
{
    [Scenario("FluentChannelAdapter ImportsAndExportsErpOrder")]
    [Fact]
    public void FluentChannelAdapter_ImportsAndExportsErpOrder()
    {
        var summary = ErpChannelAdapterExampleRunner.RunFluent(new("ERP-100", "42.50"));

        ScenarioExpect.True(summary.Imported);
        ScenarioExpect.True(summary.Exported);
        ScenarioExpect.Equal("ERP-100", summary.OrderId);
        ScenarioExpect.Equal("42.50", summary.ExternalTotal);
    }

    [Scenario("GeneratedChannelAdapter MatchesFluentAdapter")]
    [Fact]
    public void GeneratedChannelAdapter_MatchesFluentAdapter()
    {
        var generated = ErpChannelAdapterExampleRunner.RunGeneratedStatic(new("ERP-100", "42.50"));
        var fluent = ErpChannelAdapterExampleRunner.RunFluent(new("ERP-100", "42.50"));

        ScenarioExpect.Equal(fluent.Imported, generated.Imported);
        ScenarioExpect.Equal(fluent.Exported, generated.Exported);
        ScenarioExpect.Equal(fluent.OrderId, generated.OrderId);
        ScenarioExpect.Equal(fluent.ExternalTotal, generated.ExternalTotal);
    }

    [Scenario("ServiceCollection ImportsChannelAdapterExample")]
    [Fact]
    public void ServiceCollection_ImportsChannelAdapterExample()
    {
        var services = new ServiceCollection();
        services.AddErpChannelAdapterDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var service = provider.GetRequiredService<ErpChannelAdapterService>();

        var summary = service.RoundTrip(new("ERP-100", "42.50"));

        ScenarioExpect.True(summary.Imported);
        ScenarioExpect.True(summary.Exported);
        ScenarioExpect.Equal("ERP-100", summary.OrderId);
    }

    [Scenario("AggregateServiceCollection ImportsChannelAdapterExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsChannelAdapterExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<ErpChannelAdapterExampleService>();

        var summary = example.Service.RoundTrip(new("ERP-100", "42.50"));

        ScenarioExpect.True(summary.Imported);
        ScenarioExpect.True(summary.Exported);
        ScenarioExpect.Equal("ERP-100", summary.OrderId);
    }
}
