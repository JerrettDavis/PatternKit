using PatternKit.Application.WorkflowOrchestration;
using TinyBDD;

namespace PatternKit.Tests.Application.WorkflowOrchestration;

public sealed class WorkflowOrchestratorTests
{
    [Scenario("Workflow orchestrator executes ordered conditional steps with history")]
    [Fact]
    public async Task Workflow_Orchestrator_Executes_Ordered_Conditional_Steps_With_History()
    {
        var context = new WorkflowContext(requiresFraudReview: false);
        var workflow = WorkflowOrchestrator<WorkflowContext>
            .Create("checkout")
            .AddStep("capture-payment", static (ctx, _) =>
            {
                ctx.Events.Add("paid");
                return ValueTask.CompletedTask;
            }, static step => step.At(2))
            .AddStep("fraud-review", static (ctx, _) =>
            {
                ctx.Events.Add("reviewed");
                return ValueTask.CompletedTask;
            }, static step => step.At(1).When(static ctx => ctx.RequiresFraudReview))
            .Build();

        var execution = await workflow.ExecuteAsync(context);

        ScenarioExpect.Equal(WorkflowExecutionStatus.Completed, execution.Status);
        ScenarioExpect.Equal(["paid"], context.Events);
        ScenarioExpect.Equal(["fraud-review", "capture-payment"], workflow.Steps.Select(static step => step.Name).ToArray());
        ScenarioExpect.Equal(WorkflowExecutionRecordKind.Skipped, execution.History[0].Kind);
        ScenarioExpect.Equal(WorkflowExecutionRecordKind.Completed, execution.History[1].Kind);
    }

    [Scenario("Workflow orchestrator retries failures and compensates completed work")]
    [Fact]
    public async Task Workflow_Orchestrator_Retries_Failures_And_Compensates_Completed_Work()
    {
        var attempts = 0;
        var context = new WorkflowContext(requiresFraudReview: true);
        var workflow = WorkflowOrchestrator<WorkflowContext>
            .Create("fulfillment")
            .AddStep("reserve-inventory", static (ctx, _) =>
            {
                ctx.Events.Add("reserved");
                return ValueTask.CompletedTask;
            }, static step => step.Compensate(static (ctx, _) =>
            {
                ctx.Events.Add("released");
                return ValueTask.CompletedTask;
            }).At(1))
            .AddStep("capture-payment", (_, _) =>
            {
                attempts++;
                throw new InvalidOperationException("payment gateway unavailable");
            }, static step => step.At(2).WithMaxAttempts(2))
            .Build();

        var execution = await workflow.ExecuteAsync(context);

        ScenarioExpect.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        ScenarioExpect.Equal(["reserved", "released"], context.Events);
        ScenarioExpect.Equal(2, attempts);
        ScenarioExpect.Contains(execution.History, static record => record.Kind == WorkflowExecutionRecordKind.Retried);
        ScenarioExpect.Contains(execution.History, static record => record.Kind == WorkflowExecutionRecordKind.Failed);
        ScenarioExpect.Contains(execution.History, static record => record.Kind == WorkflowExecutionRecordKind.Compensated);
    }

    [Scenario("Workflow orchestrator records compensation failures")]
    [Fact]
    public void Workflow_Orchestrator_Records_Compensation_Failures()
    {
        var workflow = WorkflowOrchestrator<WorkflowContext>
            .Create()
            .AddStep("reserve", static (_, _) => ValueTask.CompletedTask, static step => step.At(1).Compensate(static (_, _) => throw new InvalidOperationException("release failed")))
            .AddStep("ship", static (_, _) => throw new InvalidOperationException("shipment failed"), static step => step.At(2))
            .Build();

        var execution = workflow.Execute(new WorkflowContext(requiresFraudReview: true));

        ScenarioExpect.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        ScenarioExpect.Contains(execution.History, static record => record.Kind == WorkflowExecutionRecordKind.CompensationFailed);
    }

    [Scenario("Workflow orchestration result types expose execution metadata")]
    [Fact]
    public async Task Workflow_Orchestration_Result_Types_Expose_Execution_Metadata()
    {
        using var cancellation = new CancellationTokenSource();
        var workflow = WorkflowOrchestrator<WorkflowContext>
            .Create("metadata")
            .AddStep("reserve", static (_, _) => ValueTask.CompletedTask, static step => step.At(7).WithMaxAttempts(2).Compensate(static (_, _) => ValueTask.CompletedTask))
            .Build();

        var execution = await workflow.ExecuteAsync(new WorkflowContext(requiresFraudReview: false), cancellation.Token);
        var step = ScenarioExpect.Single(workflow.Steps);
        var completed = ScenarioExpect.Single(execution.History);
        var retry = WorkflowExecutionRecord.Retried("reserve", 1, new InvalidOperationException("retry"));
        var failed = WorkflowExecutionRecord.Failed("reserve", 2, new InvalidOperationException("failed"));
        var compensationFailed = WorkflowExecutionRecord.CompensationFailed("reserve", new InvalidOperationException("compensation"));

        ScenarioExpect.Equal("metadata", execution.WorkflowName);
        ScenarioExpect.False(execution.Context.RequiresFraudReview);
        ScenarioExpect.Equal("reserve", step.Name);
        ScenarioExpect.Equal(7, step.Order);
        ScenarioExpect.Equal(2, step.MaxAttempts);
        ScenarioExpect.True(step.HasCompensation);
        ScenarioExpect.Equal(WorkflowExecutionRecordKind.Completed, completed.Kind);
        ScenarioExpect.Equal(1, completed.Attempt);
        ScenarioExpect.Equal("retry", retry.ErrorMessage);
        ScenarioExpect.Equal("failed", failed.ErrorMessage);
        ScenarioExpect.Equal("compensation", compensationFailed.ErrorMessage);
        ScenarioExpect.Throws<ArgumentNullException>(() => new WorkflowExecution<WorkflowContext>("bad", new WorkflowContext(false), WorkflowExecutionStatus.Completed, null!));
    }

    [Scenario("Workflow orchestrator honors cancellation before work starts")]
    [Fact]
    public async Task Workflow_Orchestrator_Honors_Cancellation_Before_Work_Starts()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var workflow = WorkflowOrchestrator<WorkflowContext>
            .Create()
            .AddStep("never", static (_, _) => throw new InvalidOperationException("should not run"))
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => workflow.ExecuteAsync(new WorkflowContext(false), cancellation.Token).AsTask());
    }

    [Scenario("Workflow orchestrator rejects invalid configuration")]
    [Fact]
    public void Workflow_Orchestrator_Rejects_Invalid_Configuration()
    {
        var builder = WorkflowOrchestrator<WorkflowContext>.Create("checkout");

        ScenarioExpect.Throws<ArgumentException>(() => WorkflowOrchestrator<WorkflowContext>.Create("").AddStep("step", static (_, _) => ValueTask.CompletedTask).Build());
        ScenarioExpect.Throws<ArgumentException>(() => WorkflowOrchestrator<WorkflowContext>.Create().Build());
        ScenarioExpect.Throws<ArgumentException>(() => builder.AddStep("", static (_, _) => ValueTask.CompletedTask));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.AddStep("missing", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.AddStep("condition", static (_, _) => ValueTask.CompletedTask, static step => step.When(null!)));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.AddStep("compensation", static (_, _) => ValueTask.CompletedTask, static step => step.Compensate(null!)));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => builder.AddStep("attempts", static (_, _) => ValueTask.CompletedTask, static step => step.WithMaxAttempts(0)));
        ScenarioExpect.Throws<InvalidOperationException>(() => WorkflowOrchestrator<WorkflowContext>.Create().AddStep("same", static (_, _) => ValueTask.CompletedTask).AddStep("same", static (_, _) => ValueTask.CompletedTask).Build());
    }

    private sealed class WorkflowContext(bool requiresFraudReview)
    {
        public bool RequiresFraudReview { get; } = requiresFraudReview;

        public List<string> Events { get; } = [];
    }
}
