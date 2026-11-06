# Workflow Orchestration Generator

The Workflow Orchestration generator creates a strongly typed factory from a partial host type and annotated workflow methods.

```csharp
using PatternKit.Generators.WorkflowOrchestration;

[WorkflowOrchestration(FactoryMethodName = "CreateGenerated", WorkflowName = "fulfillment-orchestration")]
public static partial class GeneratedFulfillmentWorkflow
{
    [WorkflowStep("reserve-inventory", 1, Compensation = nameof(ReleaseInventory))]
    private static ValueTask ReserveInventory(FulfillmentContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    [WorkflowStep("capture-payment", 2, MaxAttempts = 2)]
    private static ValueTask CapturePayment(FulfillmentContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    private static ValueTask ReleaseInventory(FulfillmentContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
```

Generated output:

```csharp
WorkflowOrchestrator<FulfillmentContext> workflow = GeneratedFulfillmentWorkflow.CreateGenerated();
```

Step methods must accept `(TContext, CancellationToken)` and return `ValueTask`. Optional condition methods accept `TContext` and return `bool`. Optional compensation methods use the same signature as a step.

## Diagnostics

- `PKWO001`: the workflow host type must be partial.
- `PKWO002`: the workflow must declare at least one `[WorkflowStep]` method.
- `PKWO003`: step, condition, or compensation method signatures are invalid.
- `PKWO004`: workflow step names or orders are duplicated.
- `PKWO005`: `FactoryMethodName` and `WorkflowName` must be non-empty.
