# Template Method Async Demo

This demo shows non-trivial, end-to-end async workflows with PatternKit’s async Template variants, including cancellation, concurrency control, and error observation.

## Async subclassing demo: AsyncDataPipeline
A 3-stage pipeline (fetch → transform → store) with optional serialization and cancellation.

```csharp
public sealed class AsyncDataPipeline : AsyncTemplateMethod<int, string>
{
    protected override bool Synchronized => false; // enable only when shared mutable state requires it

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
        return default;
    }
}

var pipe = new AsyncDataPipeline();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
var outVal = await pipe.ExecuteAsync(42, cts.Token);
```

## Async fluent demo: TemplateAsyncFluentDemo
Same shape using the fluent builder with multiple hooks and error handling.

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
    .Synchronized(false)
    .Build();

var (ok, res, err) = await tpl.TryExecuteAsync(42);
Console.WriteLine(ok ? $"OK: {res}" : $"ERR: {err}");
```

## Guidance
- Prefer async for I/O-bound steps or where cancellation must be respected.
- Use `.Synchronized()` sparingly; it introduces a critical section. Keep steps idempotent and fast.
- Use `TryExecuteAsync` to keep control flow non-throwing and centralize error observation.

## See Also
- [Template Method Pattern](../patterns/behavioral/template/templatemethod.md)
- Synchronous demo: [Template Method Demo](template-method-demo.md)

