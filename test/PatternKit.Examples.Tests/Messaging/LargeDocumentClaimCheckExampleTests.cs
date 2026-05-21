using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class LargeDocumentClaimCheckExampleTests
{
    [Scenario("Fluent large document claim check stores and restores documents")]
    [Fact]
    public void FluentLargeDocumentClaimCheck_Stores_And_Restores_Documents()
    {
        var summary = LargeDocumentClaimCheckExample.RunFluent();

        ScenarioExpect.True(summary.Restored);
        ScenarioExpect.Equal("fluent", summary.Path);
        ScenarioExpect.Equal("order-doc:doc:doc-100", summary.ClaimId);
        ScenarioExpect.Equal("document-archive", summary.StoreName);
        ScenarioExpect.Equal("doc-100", summary.DocumentId);
        ScenarioExpect.Equal("tenant-a", summary.TenantId);
        ScenarioExpect.Equal("doc-100", summary.CorrelationId);
    }

    [Scenario("Generated large document claim check preserves the workflow contract")]
    [Fact]
    public void GeneratedLargeDocumentClaimCheck_Preserves_The_Workflow_Contract()
    {
        var summary = LargeDocumentClaimCheckExample.RunGenerated();

        ScenarioExpect.True(summary.Restored);
        ScenarioExpect.Equal("source-generated", summary.Path);
        ScenarioExpect.Equal("order-doc:doc:doc-100", summary.ClaimId);
        ScenarioExpect.Equal("document-archive", summary.StoreName);
    }

    [Scenario("Large document claim check is importable through IServiceCollection")]
    [Fact]
    public void LargeDocumentClaimCheck_Is_Importable_Through_IServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddLargeDocumentClaimCheckExample()
            .BuildServiceProvider();

        var workflow = provider.GetRequiredService<LargeDocumentWorkflow>();
        var summary = workflow.Process(LargeDocumentClaimCheckExample.CreateDocumentMessage("doc-200"));

        ScenarioExpect.True(summary.Restored);
        ScenarioExpect.Equal("di", summary.Path);
        ScenarioExpect.Equal("doc-200", summary.DocumentId);
        ScenarioExpect.Equal("doc-200", summary.CorrelationId);
    }
}
