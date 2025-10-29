# AsyncVisitor

Asynchronous visitors mirror the fluent API and use `ValueTask` + `CancellationToken`. They’re ideal for I/O‑bound operations (DB calls, HTTP) keyed by runtime type.

---

## TL;DR Example (Async Result)

```csharp
var v = AsyncVisitor<Node, string>
    .Create()
    .On<Number>((n, _) => new ValueTask<string>($"#{n.Value}"))
    .Default((_, _) => new ValueTask<string>("?"))
    .Build();

var (ok, res) = await v.TryVisitAsync(new Number(7));
```

Async action variant: `AsyncActionVisitor<TBase>`.

---

## APIs

- `ValueTask<TResult> VisitAsync(TBase node, CancellationToken ct)` — runs first matching handler or default; throws on no‑match without default.
- `ValueTask<(bool ok, TResult result)> TryVisitAsync(TBase node, CancellationToken ct)` — non‑throwing path.
- `AsyncActionVisitor<TBase>` provides `ValueTask VisitAsync(...)` and `ValueTask<bool> TryVisitAsync(...)`.

Builder registration
- `.On<T>(Func<T, CancellationToken, ValueTask<TResult>> handler)`
- `.On<T>(Func<T, TResult> handler)` — sync adapter
- `.On<T>(TResult constant)`
- `.Default(Handler)` or `.Default(Func<TBase, TResult>)` (sync adapter)

---

## Cancellation And Responsiveness

- Always pass the `CancellationToken` down to async handlers.
- Throw early on cancellation to avoid wasted work.
- Avoid blocking or long‑running sync work in async handlers to keep threads available.

See cancellation test: `test/PatternKit.Tests/Behavioral/AsyncVisitorTests.cs:41`.

---

## Performance

- `ValueTask` avoids extra allocations for already‑completed operations.
- Dispatch remains a simple indexed loop over arrays of predicates/handlers.
- Built instances are immutable and safe to share across requests.

---

## Composition & DI

Register built visitors as singletons; capture dependencies in closures. Prefer passing `CancellationToken` down.

```csharp
// Registration
services.AddSingleton<AsyncVisitor<Tender, string>>(sp =>
{
    var brandSvc = sp.GetRequiredService<IBrandService>();
    return AsyncVisitor<Tender, string>
        .Create()
        .On<Card>(async (t, ct) => $"{await brandSvc.ResolveAsync(t.Brand, ct)} ****{t.Last4}")
        .On<Cash>((t, _) => new ValueTask<string>($"Cash {t.Value:C}"))
        .Default((t, _) => new ValueTask<string>($"Other {t.Amount:C}"))
        .Build();
});

// Usage
public sealed class ReceiptService(AsyncVisitor<Tender, string> renderer)
{
    public ValueTask<string> LineForAsync(Tender t, CancellationToken ct) => renderer.VisitAsync(t, ct);
}
```

Notes
- Built visitors are immutable; builders are not thread‑safe.
- Use sync adapters for quick prototyping; move to async handlers for I/O.

---

## Example Tests

`test/PatternKit.Tests/Behavioral/AsyncVisitorTests.cs:16` covers async result visitor dispatch and try semantics. The async action variant is covered at `test/PatternKit.Tests/Behavioral/AsyncVisitorTests.cs:69`.

---

## See Also

- `Visitor<TBase, TResult>` — synchronous result variant
- `AsyncActionVisitor<TBase>` — asynchronous actions
 - Examples: `docs/examples/event-processor-visitor.md:1`, `docs/examples/message-router-visitor.md:1`, `docs/examples/api-exception-mapping-visitor.md:1`
