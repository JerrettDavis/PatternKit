# Template Method Pattern

**Category:** Behavioral

## Overview
Template Method defines the skeleton of an algorithm in a base class, allowing specific steps to be customized without changing the overall structure. PatternKit offers two complementary shapes:

- Subclassing API: derive from `TemplateMethod<TContext, TResult>` and override hooks.
- Fluent API: compose a `Template<TContext, TResult>` with `Before/After/OnError/Synchronized` and `Execute/TryExecute`.

Common traits:
- Generic and type-safe (any context/result types)
- Allocation-light, production-shaped APIs
- Optional synchronization for thread safety
- Clear separation of “when/where” (hooks) and “what” (the main step)

## Structure
- `TemplateMethod<TContext, TResult>` (abstract)
  - `Execute(context)` — calls `OnBefore`, `Step`, `OnAfter` in order
  - `protected virtual void OnBefore(context)` — optional pre-step hook
  - `protected abstract TResult Step(context)` — required main step
  - `protected virtual void OnAfter(context, result)` — optional post-step hook
  - `protected virtual bool Synchronized` — set to `true` to serialize executions

- `Template<TContext, TResult>` (fluent)
  - `Execute(context)` — runs before → step → after; throws on error
  - `TryExecute(context, out result, out error)` — non-throwing path
  - `Create(step)` → `.Before(...)` → `.After(...)` → `.OnError(...)` → `.Synchronized()` → `.Build()`

## Subclassing Example
```csharp
public sealed class DataProcessor : TemplateMethod<string, int>
{
    protected override void OnBefore(string context)
        => Console.WriteLine($"Preparing to process: {context}");

    protected override int Step(string context)
        => context.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    protected override void OnAfter(string context, int result)
        => Console.WriteLine($"Processed '{context}' with result: {result}");

    // Optional: serialize concurrent Execute calls
    protected override bool Synchronized => true;
}

var processor = new DataProcessor();
var count = processor.Execute("The quick brown fox");
```

## Fluent Example
```csharp
var template = Template<string, int>
    .Create(ctx => ctx.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
    .Before(ctx => Console.WriteLine($"[Before] '{ctx}'"))
    .After((ctx, res) => Console.WriteLine($"[After] '{ctx}' -> {res}"))
    .OnError((ctx, err) => Console.WriteLine($"[Error] '{ctx}': {err}"))
    .Synchronized() // optional
    .Build();

if (template.TryExecute("The quick brown fox", out var result, out var error))
    Console.WriteLine($"Words: {result}");
else
    Console.WriteLine($"Failed: {error}");
```

## Async variants
PatternKit also provides first-class async variants with cancellation and optional synchronization:

- `AsyncTemplateMethod<TContext, TResult>` (abstract)
  - `ExecuteAsync(context, cancellationToken)` — calls `OnBeforeAsync`, `StepAsync`, `OnAfterAsync` in order
  - `protected virtual ValueTask OnBeforeAsync(context, ct)` — optional pre-step hook
  - `protected abstract ValueTask<TResult> StepAsync(context, ct)` — required main step
  - `protected virtual ValueTask OnAfterAsync(context, result, ct)` — optional post-step hook
  - `protected virtual bool Synchronized` — set to `true` to serialize `ExecuteAsync` calls (uses `SemaphoreSlim`)

- `AsyncTemplate<TContext, TResult>` (fluent)
  - `ExecuteAsync(context, ct)` — runs before → step → after; throws on error
  - `TryExecuteAsync(context, ct)` — returns `(ok, result, error)` without throwing
  - `Create(async (ctx, ct) => ...)` → `.Before(...)`/`.After(...)`/`.OnError(...)` (async or sync overloads) → `.Synchronized()` → `.Build()`

### Async subclassing example
```csharp
public sealed class AsyncDataPipeline : AsyncTemplateMethod<int, string>
{
    protected override bool Synchronized => false; // enable for strict serialization

    protected override async ValueTask OnBeforeAsync(int id, CancellationToken ct)
    {
        Console.WriteLine($"[BeforeAsync] {id}");
        await Task.Yield();
    }

    protected override async ValueTask<string> StepAsync(int id, CancellationToken ct)
    {
        await Task.Delay(25, ct); // fetch
        await Task.Delay(10, ct); // transform
        await Task.Delay(5, ct);  // store
        return $"VAL-{id}";
    }

    protected override ValueTask OnAfterAsync(int id, string result, CancellationToken ct)
    {
        Console.WriteLine($"[AfterAsync] {id} -> {result}");
        return default; // completed
    }
}

var pipe = new AsyncDataPipeline();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
var outVal = await pipe.ExecuteAsync(42, cts.Token);
```

### Async fluent example
```csharp
var tpl = AsyncTemplate<int, string>
    .Create(async (id, ct) =>
    {
        await Task.Delay(15, ct);
        if (id < 0) throw new InvalidOperationException("invalid id");
        return $"VAL-{id}";
    })
    .Before((id, ct) => { Console.WriteLine($"[BeforeAsync] {id}"); return default; })
    .After((id, res, ct) => { Console.WriteLine($"[AfterAsync] {id} -> {res}"); return default; })
    .OnError((id, err, ct) => { Console.WriteLine($"[ErrorAsync] {id}: {err}"); return default; })
    .Synchronized() // optional
    .Build();

var (ok, result, error) = await tpl.TryExecuteAsync(42);
```

### Guidance
- Prefer async variants for I/O-bound steps or when cancellation needs to flow end-to-end.
- Use `.Synchronized()` or override `Synchronized` only when shared mutable state demands serialization.
- Choose `TryExecuteAsync` when you need non-throwing control flow and centralized error observation.

## When to Use
- You need a consistent workflow with customizable steps.
- You want to prevent structural drift while enabling tailored behaviors.
- You need optional error handling and synchronization without external plumbing.

## Thread Safety
- Subclassing: override `Synchronized` to serialize `Execute` calls via a per-instance lock.
- Fluent: call `.Synchronized()` on the builder to enable a per-instance lock.
- For stateless or externally synchronized code, leave synchronization off for maximal concurrency.

## Error Handling
- Subclassing: let exceptions bubble; catch externally if needed.
- Fluent: use `TryExecute` to avoid throwing, and `.OnError(...)` to observe errors.

## Related Patterns
- Strategy: swap entire algorithms rather than customizing steps inline.
- Chain of Responsibility: linear rule packs with stop/continue semantics.
- State: behavior that changes with state; Template Method keeps structure fixed.

## See Also
- Refactoring Guru: Template Method — https://refactoring.guru/design-patterns/template-method
- Examples: see the Template Method demos in the Examples section.
