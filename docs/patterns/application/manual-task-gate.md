# Human Approval / Manual Task Gate

Manual Task Gate tracks human-owned work that blocks a workflow until a person approves, rejects, cancels, or completes the task. Use it for high-value order reviews, exception handling, finance approvals, manual fraud checks, and operations where automated orchestration must pause on an explicit decision.

`ManualTaskGate<TKey>` provides the fluent runtime path:

```csharp
var gate = ManualTaskGate<Guid>
    .Create("order-approval-gate")
    .Build();

gate.Open(orderId, "Approve high value order", "order-approvals", requestId);
var decision = gate.Approve(orderId, "case-manager", "Approved for fulfillment.");
var state = gate.GetGateState();
```

The gate remains blocked while any task has `ManualTaskStatus.Pending`. Decisions are retained in the snapshot so operators can inspect which tasks were approved, rejected, or canceled.

Use the source-generated path when you want a reusable named gate factory:

```csharp
[GenerateManualTaskGate(typeof(Guid), FactoryMethodName = "CreateGenerated", GateName = "order-approval-gate")]
public static partial class GeneratedOrderApprovalManualTaskGate;
```

The generated factory returns the same `ManualTaskGate<Guid>` fluent object without handwritten boilerplate.
