using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class SupplierQuoteScatterGatherExampleTests
{
    [Scenario("FluentScatterGather SelectsBestSupplierQuote")]
    [Fact]
    public void FluentScatterGather_SelectsBestSupplierQuote()
    {
        var summary = SupplierQuoteScatterGatherExampleRunner.RunFluent(new("sku-1", 120, false));

        ScenarioExpect.Equal(2, summary.AcceptedQuotes);
        ScenarioExpect.Equal("regional", summary.BestSupplier);
        ScenarioExpect.Equal(9.75m, summary.BestUnitPrice);
    }

    [Scenario("GeneratedScatterGather MatchesFluentQuoteSelection")]
    [Fact]
    public void GeneratedScatterGather_MatchesFluentQuoteSelection()
    {
        var request = new SupplierQuoteRequest("sku-1", 5, true);
        var fluent = SupplierQuoteScatterGatherExampleRunner.RunFluent(request);
        var generated = new SupplierQuoteService(GeneratedSupplierQuoteScatterGather.Create()).RequestQuotes(request);

        ScenarioExpect.Equal(fluent.AcceptedQuotes, generated.AcceptedQuotes);
        ScenarioExpect.Equal(fluent.BestSupplier, generated.BestSupplier);
        ScenarioExpect.Equal(fluent.BestUnitPrice, generated.BestUnitPrice);
    }

    [Scenario("ServiceCollection ImportsScatterGatherExample")]
    [Fact]
    public void ServiceCollection_ImportsScatterGatherExample()
    {
        var services = new ServiceCollection();
        services.AddSupplierQuoteScatterGatherDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var service = provider.GetRequiredService<SupplierQuoteService>();

        var summary = service.RequestQuotes(new("sku-1", 120, false));

        ScenarioExpect.Equal("regional", summary.BestSupplier);
    }

    [Scenario("AggregateServiceCollection ImportsScatterGatherExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsScatterGatherExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<SupplierQuoteScatterGatherExampleService>();

        var summary = example.Service.RequestQuotes(new("sku-1", 120, false));

        ScenarioExpect.Equal("regional", summary.BestSupplier);
    }
}
