using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "RecipientList")]
public class RecipientListBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create recipient list")]
    [BenchmarkCategory("Fluent", "Construction")]
    public RecipientList<GeneratedShipmentEvent> Fluent_CreateRecipientList()
        => RecipientList<GeneratedShipmentEvent>.Create()
            .When("priority-audit", static (message, _) => message.Payload.Priority == "priority")
            .Then(static (_, _) => { })
            .When("billing-ledger", static (message, _) => message.Payload.Total >= 100m)
            .Then(static (_, _) => { })
            .Build();

    [Benchmark(Description = "Generated: create recipient list")]
    [BenchmarkCategory("Generated", "Construction")]
    public RecipientList<GeneratedShipmentEvent> Generated_CreateRecipientList()
        => GeneratedShipmentRecipients.Create();

    [Benchmark(Description = "Fluent: dispatch shipment event")]
    [BenchmarkCategory("Fluent", "Execution")]
    public RecipientListSummary Fluent_DispatchShipmentEvent()
        => RecipientListGeneratorExample.RunFluent();

    [Benchmark(Description = "Generated: dispatch shipment event")]
    [BenchmarkCategory("Generated", "Execution")]
    public RecipientListSummary Generated_DispatchShipmentEvent()
        => RecipientListGeneratorExample.RunGenerated();
}
