using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ClaimCheck")]
public class ClaimCheckBenchmarks
{
    private static readonly Message<LargeOrderDocument> Document = LargeDocumentClaimCheckExample.CreateDocumentMessage("doc-100");

    [Benchmark(Baseline = true, Description = "Fluent: create claim check")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ClaimCheck<LargeOrderDocument> Fluent_CreateClaimCheck()
        => LargeDocumentClaimCheckPolicies.CreateFluentClaimCheck(new InMemoryClaimCheckStore<LargeOrderDocument>());

    [Benchmark(Description = "Generated: create claim check")]
    [BenchmarkCategory("Generated", "Construction")]
    public ClaimCheck<LargeOrderDocument> Generated_CreateClaimCheck()
        => GeneratedLargeDocumentClaimCheck.Create();

    [Benchmark(Description = "Fluent: store and restore large document")]
    [BenchmarkCategory("Fluent", "Execution")]
    public LargeDocumentClaimCheckSummary Fluent_StoreAndRestoreLargeDocument()
        => new LargeDocumentWorkflow(LargeDocumentClaimCheckPolicies.CreateFluentClaimCheck(new InMemoryClaimCheckStore<LargeOrderDocument>()))
            .Process(Document);

    [Benchmark(Description = "Generated: store and restore large document")]
    [BenchmarkCategory("Generated", "Execution")]
    public LargeDocumentClaimCheckSummary Generated_StoreAndRestoreLargeDocument()
        => new LargeDocumentWorkflow(GeneratedLargeDocumentClaimCheck.Create()).Process(Document);
}
