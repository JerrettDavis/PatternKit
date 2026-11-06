# Fulfillment Workflow Orchestration

This example models a production fulfillment workflow with inventory reservation, optional fraud review, payment capture, warehouse release, retry behavior, and compensation.

The fluent path builds the workflow directly:

```csharp
var workflow = FulfillmentWorkflowOrchestrations.CreateFluent();
```

The generated path uses annotated workflow methods:

```csharp
var workflow = GeneratedFulfillmentWorkflowOrchestration.CreateGenerated();
```

The example is importable through standard dependency injection:

```csharp
services.AddFulfillmentWorkflowOrchestrationDemo();
```

`FulfillmentWorkflowOrchestrationDemoRunner` returns a summary containing the final status, domain events, and workflow history. Production applications can attach the history to audit logs, traces, or outbox messages while keeping the orchestration itself explicit and testable.
