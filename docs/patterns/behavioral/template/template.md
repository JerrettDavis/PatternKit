# Template<TContext, TResult>

A fluent, allocation-light Template: define a fixed workflow (before → step → after), add optional hooks, opt into synchronization, and choose throwing or non-throwing execution.

---

## What it is

- Skeleton with three phases: Before (0..n), Step (1), After (0..n)
- Non-throwing path via TryExecute(context, out result, out error)
- Optional per-instance synchronization (mutual exclusion)
- Immutable and thread-safe after Build()

---

## TL;DR

```csharp
using PatternKit.Behavioral.Template;

var tpl = Template<string, int>
    .Create(ctx => ctx.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
    .Before(ctx => Console.WriteLine($"[Before] '{ctx}'"))
    .After((ctx, res) => Console.WriteLine($"[After] '{ctx}' -> {res}"))
    .OnError((ctx, err) => Console.WriteLine($"[Error] '{ctx}': {err}"))
    .Synchronized() // optional
    .Build();

var ok = tpl.TryExecute("The quick brown fox", out var words, out var error);
Console.WriteLine(ok ? $"Words={words}" : $"Failed: {error}");
```

---

## API shape

```csharp
var tpl = Template<TContext, TResult>
    .Create(static (TContext ctx) => /* TResult */)
    .Before(static (TContext ctx) => { /* side-effect */ })            // 0..n
    .After(static (TContext ctx, TResult res) => { /* side-effect */ }) // 0..n
    .OnError(static (TContext ctx, string error) => { /* observe */ })  // 0..n
    .Synchronized()                                                     // optional
    .Build();

// Execute throws on failure
TResult result = tpl.Execute(context);

// TryExecute returns false and calls OnError hooks rather than throwing
bool ok = tpl.TryExecute(context, out TResult result, out string? error);
```

Notes
- Multiple Before/After/OnError hooks compose; registration order is call order.
- OnError hooks run only when TryExecute catches an exception.
- Synchronized() uses a per-instance lock; keep steps short to avoid contention.

---

## Testing (TinyBDD-style)

```csharp
using PatternKit.Behavioral.Template;
using TinyBDD;
using TinyBDD.Xunit;

var (tpl, calls) = (
    Template<string, int>
        .Create(ctx => { calls.Enqueue($"step:{ctx}"); return ctx.Length; })
        .Before(ctx => calls.Enqueue($"before:{ctx}"))
        .After((ctx, res) => calls.Enqueue($"after:{ctx}:{res}"))
        .Build(),
    new System.Collections.Concurrent.ConcurrentQueue<string>());

var r = tpl.Execute("abc"); // 3
// calls: before:abc, step:abc, after:abc:3
```

---

## Design notes

- No reflection/LINQ in the hot path; simple delegate invocation and optional lock.
- Immutable after Build() so instances can be safely shared across threads.
- Hooks are multicast; avoid heavy work inside hooks.

---

## Gotchas

- Execute throws; OnError hooks are not invoked on Execute.
- TryExecute returns default(TResult) on failure and captures ex.Message as error.
- Synchronized forces mutual exclusion; prefer idempotent, fast steps.

---

## See also

- Subclassing: [TemplateMethod<TContext, TResult>](./templatemethod.md)
- Async fluent: [AsyncTemplate<TContext, TResult>](./asynctemplate.md)
- Demos: [Template Method Demo](../../../examples/template-method-demo.md), [Template Method Async Demo](../../../examples/template-method-async-demo.md)
