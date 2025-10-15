# AsyncTemplate<TContext, TResult>

A fluent, allocation-light async Template: define a fixed workflow (before → step → after), add async/sync hooks, opt into synchronization, and choose throwing or non-throwing execution.

---

## What it is

- Async skeleton with three phases: Before (0..n), Step (1), After (0..n)
- Non-throwing path via `TryExecuteAsync(context)` returning `(ok, result, error)`
- Optional per-instance synchronization via `SemaphoreSlim`
- Immutable and thread-safe after `Build()`

---

## TL;DR

```csharp
using PatternKit.Behavioral.Template;

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
Console.WriteLine(ok ? $"OK: {result}" : $"ERR: {error}");
```

---

## API shape

```csharp
var tpl = AsyncTemplate<TContext, TResult>
    .Create(static (TContext ctx, CancellationToken ct) => /* ValueTask<TResult> */)
    .Before(static (TContext ctx, CancellationToken ct) => /* ValueTask */)           // 0..n (async)
    .Before(static (TContext ctx) => { /* side-effect */ })                           // 0..n (sync overload)
    .After(static (TContext ctx, TResult res, CancellationToken ct) => /* ValueTask */) // 0..n (async)
    .After(static (TContext ctx, TResult res) => { /* side-effect */ })               // 0..n (sync overload)
    .OnError(static (TContext ctx, string error, CancellationToken ct) => /* ValueTask */) // 0..n (async)
    .OnError(static (TContext ctx, string error) => { /* observe */ })                // 0..n (sync overload)
    .Synchronized()                                                                   // optional
    .Build();

// Throws on failure
TResult result = await tpl.ExecuteAsync(context, ct);

// Non-throwing; returns tuple (ok, result?, error?)
(bool ok, TResult? result, string? error) = await tpl.TryExecuteAsync(context, ct);
```

Notes
- Multiple hooks compose; registration order is invocation order.
- OnError hooks run only when TryExecuteAsync catches an exception.
- Synchronized() uses an async mutex; keep the critical section small.

---

## Testing (TinyBDD-style)

```csharp
using PatternKit.Behavioral.Template;
using TinyBDD;
using TinyBDD.Xunit;

var tpl = AsyncTemplate<string, int>
    .Create(async (ctx, ct) => { await Task.Yield(); return ctx.Length; })
    .Before((ctx, ct) => { Console.WriteLine($"before:{ctx}"); return default; })
    .After((ctx, res, ct) => { Console.WriteLine($"after:{ctx}:{res}"); return default; })
    .Build();

var r = await tpl.ExecuteAsync("abc"); // 3
```

---

## Design notes

- No reflection/LINQ in the hot path; simple async delegate calls and an optional async lock.
- Immutable after Build() so instances can be safely shared across threads.
- Sync and async hooks both supported; they are adapted internally to async.

---

## Gotchas

- ExecuteAsync throws; OnError hooks are not invoked by ExecuteAsync.
- TryExecuteAsync captures ex.Message as error; result is default when failing.
- Synchronized serializes executions; prefer idempotent, short steps.

---

## See also

- Subclassing: [TemplateMethod<TContext, TResult>](./templatemethod.md)
- Synchronous fluent: [Template<TContext, TResult>](./template.md)
- Demos: [Template Method Demo](../../../examples/template-method-demo.md), [Template Method Async Demo](../../../examples/template-method-async-demo.md)
