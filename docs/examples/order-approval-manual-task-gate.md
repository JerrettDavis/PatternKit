# Order Approval Manual Task Gate

This example shows a high-value checkout flow that pauses on manual approval before fulfillment can continue.

```csharp
var request = new OrderApprovalRequest(orderId, "REQ-200", 1250.00m, "checkout-api");
var summary = OrderApprovalManualTaskGateDemoRunner.RunFluent(request);
```

The source-generated route uses the same workflow through a generated factory:

```csharp
var gate = GeneratedOrderApprovalManualTaskGate.CreateGenerated();
var service = new OrderApprovalManualTaskService(gate);
```

Import the demo into an existing host with:

```csharp
services.AddOrderApprovalManualTaskGateDemo();
```

The registration provides `ManualTaskGate<Guid>`, `OrderApprovalManualTaskService`, and `OrderApprovalManualTaskGateDemoRunner`.
