using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.DomainServiceDemo;
using TinyBDD;
using static PatternKit.Examples.DomainServiceDemo.ShippingDomainServiceDemo;

namespace PatternKit.Examples.Tests.DomainServiceDemo;

public sealed class ShippingDomainServiceDemoTests
{
    [Scenario("Fluent and generated domain services select the same shipping decisions")]
    [Fact]
    public void Fluent_And_Generated_Domain_Services_Select_The_Same_Shipping_Decisions()
    {
        var standard = CreateStandardRequest();
        var highValue = CreateHighValueRequest();
        var fluent = CreateFluentRegistry();
        var generated = CreateGeneratedRegistry();

        ScenarioExpect.Equal(SelectBest(standard, fluent), SelectBest(standard, generated));
        ScenarioExpect.Equal(SelectBest(highValue, fluent), SelectBest(highValue, generated));
        ScenarioExpect.Equal(["ground", "insured-air"], generated.Names.ToArray());
    }

    [Scenario("Generated domain service explains carrier and insurance choice")]
    [Fact]
    public void Generated_Domain_Service_Explains_Carrier_And_Insurance_Choice()
    {
        var service = new ShippingDomainService(CreateGeneratedRegistry());

        var decision = service.SelectBest(CreateHighValueRequest());

        ScenarioExpect.Equal("air", decision.Carrier);
        ScenarioExpect.True(decision.Insured);
        ScenarioExpect.Equal(38m, decision.Cost);
    }

    [Scenario("Shipping domain service integrates with IServiceCollection")]
    [Fact]
    public void Shipping_Domain_Service_Integrates_With_IServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddShippingDomainServiceDemo();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var service = provider.GetRequiredService<ShippingDomainService>();
        var decision = service.SelectBest(CreateStandardRequest());

        ScenarioExpect.Equal("ground", decision.Carrier);
        ScenarioExpect.False(decision.Insured);
    }

    [Scenario("Shipping domain service is importable through AddPatternKitExamples")]
    [Fact]
    public void Shipping_Domain_Service_Is_Importable_Through_AddPatternKitExamples()
    {
        using var provider = new ServiceCollection()
            .AddPatternKitExamples()
            .BuildServiceProvider(validateScopes: true);

        var example = provider.GetRequiredService<ShippingDomainServicePatternExample>();
        var decision = example.Service.SelectBest(CreateHighValueRequest());

        ScenarioExpect.Equal("air", decision.Carrier);
        ScenarioExpect.True(decision.Insured);
    }
}
