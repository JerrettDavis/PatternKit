using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "DeadLetterChannel")]
public class DeadLetterChannelBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create dead-letter channel")]
    [BenchmarkCategory("Fluent", "Construction")]
    public DeadLetterChannel<FulfillmentCommand> Fluent_CreateDeadLetterChannel()
        => FulfillmentDeadLetterPolicies.CreateFluentChannel(new InMemoryDeadLetterStore<FulfillmentCommand>());

    [Benchmark(Description = "Generated: create dead-letter channel")]
    [BenchmarkCategory("Generated", "Construction")]
    public DeadLetterChannel<FulfillmentCommand> Generated_CreateDeadLetterChannel()
        => GeneratedFulfillmentDeadLetters.CreateChannel();

    [Benchmark(Description = "Fluent: capture and prepare replay")]
    [BenchmarkCategory("Fluent", "Execution")]
    public FulfillmentDeadLetterSummary Fluent_CaptureAndPrepareReplay()
        => FulfillmentDeadLetterChannelExample.RunFluent();

    [Benchmark(Description = "Generated: capture and prepare replay")]
    [BenchmarkCategory("Generated", "Execution")]
    public FulfillmentDeadLetterSummary Generated_CaptureAndPrepareReplay()
        => FulfillmentDeadLetterChannelExample.RunGenerated();
}
