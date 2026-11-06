using BenchmarkDotNet.Attributes;
using PatternKit.Application.WorkflowOrchestration;
using PatternKit.Examples.WorkflowOrchestrationDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "WorkflowOrchestration")]
public class WorkflowOrchestrationBenchmarks
{
    private static readonly FulfillmentRequest Request = new("ORDER-100", RequiresFraudReview: true, PaymentShouldFail: false);

    [Benchmark(Baseline = true, Description = "Fluent: create workflow orchestration")]
    [BenchmarkCategory("Fluent", "Construction")]
    public WorkflowOrchestrator<FulfillmentWorkflowContext> Fluent_CreateWorkflowOrchestration()
        => FulfillmentWorkflowOrchestrations.CreateFluent();

    [Benchmark(Description = "Generated: create workflow orchestration")]
    [BenchmarkCategory("Generated", "Construction")]
    public WorkflowOrchestrator<FulfillmentWorkflowContext> Generated_CreateWorkflowOrchestration()
        => GeneratedFulfillmentWorkflowOrchestration.CreateGenerated();

    [Benchmark(Description = "Fluent: execute fulfillment workflow orchestration")]
    [BenchmarkCategory("Fluent", "Execution")]
    public FulfillmentSummary Fluent_ExecuteFulfillmentWorkflow()
        => FulfillmentWorkflowOrchestrationDemoRunner.RunFluentAsync(Request).AsTask().GetAwaiter().GetResult();

    [Benchmark(Description = "Generated: execute fulfillment workflow orchestration")]
    [BenchmarkCategory("Generated", "Execution")]
    public FulfillmentSummary Generated_ExecuteFulfillmentWorkflow()
        => FulfillmentWorkflowOrchestrationDemoRunner.RunGeneratedStaticAsync(Request).AsTask().GetAwaiter().GetResult();
}
