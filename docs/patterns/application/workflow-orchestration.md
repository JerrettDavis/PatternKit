# Workflow Orchestration

Workflow Orchestration defines an explicit ordered workflow for multi-step application work. It is useful when the application must run steps in a known order, skip conditional work, retry transient steps, compensate already completed steps, and expose execution history for logs, tests, or operators.

`WorkflowOrchestrator<TContext>` provides the fluent runtime path:

```csharp
var workflow = WorkflowOrchestrator<FulfillmentWorkflowContext>
    .Create("fulfillment-orchestration")
    .AddStep("reserve-inventory", ReserveInventory, step => step
        .At(1)
        .Compensate(ReleaseInventory))
    .AddStep("capture-payment", CapturePayment, step => step
        .At(2)
        .WithMaxAttempts(2))
    .Build();

var execution = await workflow.ExecuteAsync(context);
```

Each execution returns a `WorkflowExecution<TContext>` with a status and ordered history records for completed, skipped, retried, failed, compensated, and compensation-failed steps.

## Use When

- A service owns a synchronous or request-scoped workflow with explicit steps.
- Step order, conditional gates, retries, and compensation should be visible in code.
- Operators or tests need a history of what ran and what was skipped or undone.

## Compare With

- Use Saga / Process Manager when the workflow spans messages, long-running correlations, or external events.
- Use Transaction Script when the operation is a simpler one-shot procedure without reusable step metadata.
- Use State Machine when the primary concern is valid state transitions rather than step execution.
- Use Routing Slip when the itinerary travels with a message through distributed handlers.
