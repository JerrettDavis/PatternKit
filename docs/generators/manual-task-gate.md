# Manual Task Gate Generator

The Manual Task Gate generator creates a strongly typed factory for `ManualTaskGate<TKey>` from a partial host type.

```csharp
using PatternKit.Generators.ManualTaskGates;

[GenerateManualTaskGate(typeof(Guid), FactoryMethodName = "CreateGenerated", GateName = "order-approval-gate")]
public static partial class GeneratedOrderApprovalManualTaskGate;
```

Generated usage:

```csharp
ManualTaskGate<Guid> gate = GeneratedOrderApprovalManualTaskGate.CreateGenerated();
```

The host type must be partial. `FactoryMethodName` and `GateName` must be non-empty when provided.
