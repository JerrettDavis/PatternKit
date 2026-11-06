using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class PartnerEventTranslatorExampleTests
{
    [Scenario("Fluent partner event translator normalizes partner order events")]
    [Fact]
    public void FluentPartnerEventTranslator_Normalizes_Partner_Order_Events()
    {
        var summary = PartnerEventTranslatorExample.RunFluent();

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal("fluent", summary.Path);
        ScenarioExpect.Equal("commerce-EXT-100", summary.OrderId);
        ScenarioExpect.Equal(125m, summary.Total);
        ScenarioExpect.Equal("partner-a", summary.SourcePartnerId);
        ScenarioExpect.Equal("EXT-100", summary.CorrelationId);
        ScenarioExpect.Equal("application/vnd.patternkit.commerce-order-accepted+json", summary.ContentType);
        ScenarioExpect.True(summary.RawSignatureRemoved);
    }

    [Scenario("Generated partner event translator preserves the production contract")]
    [Fact]
    public void GeneratedPartnerEventTranslator_Preserves_The_Production_Contract()
    {
        var summary = PartnerEventTranslatorExample.RunGenerated();

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal("source-generated", summary.Path);
        ScenarioExpect.Equal("commerce-EXT-100", summary.OrderId);
        ScenarioExpect.Equal("EXT-100", summary.CorrelationId);
        ScenarioExpect.Equal("application/vnd.patternkit.commerce-order-accepted+json", summary.ContentType);
        ScenarioExpect.True(summary.RawSignatureRemoved);
    }

    [Scenario("Partner event translator is importable through IServiceCollection")]
    [Fact]
    public void PartnerEventTranslator_Is_Importable_Through_IServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddPartnerEventTranslatorExample()
            .BuildServiceProvider();

        var service = provider.GetRequiredService<PartnerOrderImportService>();
        var summary = service.Import(PartnerEventTranslatorExample.CreatePartnerMessage("partner-b", "EXT-200", 74m));

        ScenarioExpect.True(summary.Accepted);
        ScenarioExpect.Equal("di", summary.Path);
        ScenarioExpect.Equal("commerce-EXT-200", summary.OrderId);
        ScenarioExpect.Equal("partner-b", summary.SourcePartnerId);
        ScenarioExpect.True(summary.RawSignatureRemoved);
    }
}
