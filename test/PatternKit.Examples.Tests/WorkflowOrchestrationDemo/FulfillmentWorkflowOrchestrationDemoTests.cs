using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.WorkflowOrchestration;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.WorkflowOrchestrationDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.WorkflowOrchestrationDemo;

public sealed class FulfillmentWorkflowOrchestrationDemoTests
{
    [Scenario("Fluent workflow orchestration fulfills an order")]
    [Fact]
    public async Task Fluent_Workflow_Orchestration_Fulfills_An_Order()
    {
        var summary = await FulfillmentWorkflowOrchestrationDemoRunner.RunFluentAsync(CreateRequest());

        ScenarioExpect.Equal(WorkflowExecutionStatus.Completed, summary.Status);
        ScenarioExpect.Equal(["inventory:reserved", "fraud:reviewed", "payment:captured", "warehouse:released"], summary.Events);
        ScenarioExpect.Equal(4, summary.History.Count(static kind => kind == WorkflowExecutionRecordKind.Completed));
    }

    [Scenario("Generated workflow orchestration matches fluent behavior")]
    [Fact]
    public async Task Generated_Workflow_Orchestration_Matches_Fluent_Behavior()
    {
        var request = CreateRequest();

        var fluent = await FulfillmentWorkflowOrchestrationDemoRunner.RunFluentAsync(request);
        var generated = await FulfillmentWorkflowOrchestrationDemoRunner.RunGeneratedStaticAsync(request);

        ScenarioExpect.Equal(fluent.Status, generated.Status);
        ScenarioExpect.Equal(fluent.Events, generated.Events);
        ScenarioExpect.Equal(fluent.History, generated.History);
    }

    [Scenario("Workflow orchestration compensates inventory when payment fails")]
    [Fact]
    public async Task Workflow_Orchestration_Compensates_Inventory_When_Payment_Fails()
    {
        var summary = await FulfillmentWorkflowOrchestrationDemoRunner.RunGeneratedStaticAsync(
            new FulfillmentRequest("ORDER-100", RequiresFraudReview: false, PaymentShouldFail: true));

        ScenarioExpect.Equal(WorkflowExecutionStatus.Failed, summary.Status);
        ScenarioExpect.Equal(["inventory:reserved", "inventory:released"], summary.Events);
        ScenarioExpect.Contains(summary.History, static kind => kind == WorkflowExecutionRecordKind.Retried);
        ScenarioExpect.Contains(summary.History, static kind => kind == WorkflowExecutionRecordKind.Compensated);
    }

    [Scenario("Workflow orchestration skips fraud review when it is not required")]
    [Fact]
    public async Task Workflow_Orchestration_Skips_Fraud_Review_When_It_Is_Not_Required()
    {
        var summary = await FulfillmentWorkflowOrchestrationDemoRunner.RunFluentAsync(
            new FulfillmentRequest("ORDER-100", RequiresFraudReview: false, PaymentShouldFail: false));

        ScenarioExpect.Equal(WorkflowExecutionStatus.Completed, summary.Status);
        ScenarioExpect.Equal(["inventory:reserved", "payment:captured", "warehouse:released"], summary.Events);
        ScenarioExpect.Contains(summary.History, static kind => kind == WorkflowExecutionRecordKind.Skipped);
    }

    [Scenario("Workflow orchestration context validates request")]
    [Fact]
    public void Workflow_Orchestration_Context_Validates_Request()
        => ScenarioExpect.Throws<ArgumentNullException>(() => new FulfillmentWorkflowContext(null!));

    [Scenario("ServiceCollection imports workflow orchestration example")]
    [Fact]
    public async Task ServiceCollection_Imports_Workflow_Orchestration_Example()
    {
        var services = new ServiceCollection();
        services.AddFulfillmentWorkflowOrchestrationDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<FulfillmentWorkflowOrchestrationDemoRunner>();
        var summary = await runner.RunGeneratedAsync(CreateRequest());

        ScenarioExpect.Equal(WorkflowExecutionStatus.Completed, summary.Status);
        ScenarioExpect.NotNull(provider.GetRequiredService<WorkflowOrchestrator<FulfillmentWorkflowContext>>());
    }

    [Scenario("Aggregate examples import workflow orchestration example")]
    [Fact]
    public async Task Aggregate_Examples_Import_Workflow_Orchestration_Example()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<FulfillmentWorkflowOrchestrationPatternExample>();
        var summary = await example.Runner.RunGeneratedAsync(CreateRequest());

        ScenarioExpect.Equal(WorkflowExecutionStatus.Completed, summary.Status);
        ScenarioExpect.NotNull(example.Workflow);
    }

    private static FulfillmentRequest CreateRequest()
        => new("ORDER-100", RequiresFraudReview: true, PaymentShouldFail: false);
}
