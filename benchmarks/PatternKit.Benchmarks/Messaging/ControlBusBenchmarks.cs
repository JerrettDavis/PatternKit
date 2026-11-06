using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.ControlBus;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ControlBus")]
public class ControlBusBenchmarks
{
    private static readonly FulfillmentControlCommand Command = new("pause", "processor-east");

    [Benchmark(Baseline = true, Description = "Fluent: create control bus")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ControlBus<FulfillmentControlCommand> Fluent_CreateControlBus()
        => FulfillmentControlBuses.Create(new FulfillmentProcessorControlState());

    [Benchmark(Description = "Generated: create control bus")]
    [BenchmarkCategory("Generated", "Construction")]
    public ControlBus<FulfillmentControlCommand> Generated_CreateControlBus()
    {
        FulfillmentProcessorControlRegistry.Current = new FulfillmentProcessorControlState();
        return GeneratedFulfillmentControlBus.Create();
    }

    [Benchmark(Description = "Fluent: dispatch operational command")]
    [BenchmarkCategory("Fluent", "Execution")]
    public FulfillmentControlSummary Fluent_DispatchOperationalCommand()
        => FulfillmentControlBusExampleRunner.RunFluent(Command);

    [Benchmark(Description = "Generated: dispatch operational command")]
    [BenchmarkCategory("Generated", "Execution")]
    public FulfillmentControlSummary Generated_DispatchOperationalCommand()
    {
        var state = new FulfillmentProcessorControlState();
        FulfillmentProcessorControlRegistry.Current = state;
        return new FulfillmentControlBusService(GeneratedFulfillmentControlBus.Create(), state).Execute(Command);
    }
}
