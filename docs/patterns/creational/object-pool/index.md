# Object Pool

Object Pool keeps reusable, expensive objects behind explicit leases. PatternKit's runtime API is thread-safe, bounded, and designed for services that need temporary buffers, protocol clients, encoders, or other resettable resources without allocating on every operation.

```csharp
var pool = ObjectPool<FormulaEvaluationBuffer>
    .Create()
    .WithFactory(static () => new FormulaEvaluationBuffer())
    .OnReturn(static buffer => buffer.Reset())
    .WithMaxRetained(16)
    .Build();

using var lease = pool.Rent();
lease.Value.Load(variables);
var total = lease.Value.Evaluate("subtotal + tax");
```

Use `OnReturn` to reset state, `RetainWhen` to discard unhealthy instances, and `WithMaxRetained` to prevent unbounded memory growth. Disposing the lease returns the object exactly once. Disposing the pool releases retained `IDisposable` instances.

## Source Generation

```csharp
[GenerateObjectPool(
    typeof(FormulaEvaluationBuffer),
    FactoryMethodName = "CreateGenerated",
    MaxRetained = 16,
    ResetMethodName = nameof(FormulaEvaluationBuffer.Reset))]
public static partial class SpreadsheetFormulaBufferPools;
```

The generated factory emits the same fluent API calls as handwritten code. It validates that the pool host is partial and that the pooled type can be constructed with `new T()`.

## Dependency Injection

Register the generated pool as a singleton and inject it into services that rent buffers per operation:

```csharp
services.AddSingleton(static _ => SpreadsheetFormulaBufferPools.CreateGenerated());
services.AddSingleton<SpreadsheetFormulaService>();
```

The spreadsheet formula demo in `src/PatternKit.Examples/ObjectPoolDemo` shows the production shape with `IServiceCollection`, a generated route, and TinyBDD coverage.
