# AsyncActionVisitor

Asynchronous, side‑effecting visitor that maps runtime types to `ValueTask`‑returning actions. Ideal for I/O‑bound work keyed by type (e.g., routing, enrichment, persistence).

---

## What It Is

- Type‑to‑async‑action mapping: `On<T>(Func<T, CancellationToken, ValueTask>)` with optional `.Default(...)`.
- First‑match‑wins evaluation. Put specific types before base types.
- Non‑throwing variant: `TryVisitAsync(node, ct)` returns `false` when no action/default matched.

---

## TL;DR Example

```csharp
var v = AsyncActionVisitor<Tender>
    .Create()
    .On<Cash>(async (t, ct) => await ledger.ApplyCashAsync(t, ct))
    .On<Card>(async (t, ct) => await gateway.ChargeAsync(t, ct))
    .Default(async (t, ct) => await logger.LogAsync($"Unknown: {t}", ct))
    .Build();

await v.VisitAsync(tender, ct); // throws if no match and no default
```

See core API: `src/PatternKit.Core/Behavioral/Visitor/AsyncActionVisitor.cs:6`.

---

## API

- `ValueTask VisitAsync(TBase node, CancellationToken ct)` — runs first matching action or default; throws when neither applies.
- `ValueTask<bool> TryVisitAsync(TBase node, CancellationToken ct)` — non‑throwing path.
- `Builder.On<T>(Func<T, CancellationToken, ValueTask>)` — registers a type‑specific async action.
- `Builder.On<T>(Action<T>)` — sync adapter.
- `Builder.Default(ActionHandler)` or `Builder.Default(Action<TBase>)` — fallback action.

---

## Composition & DI

Register built visitors as singletons; capture dependencies via closures or provide a factory.

```csharp
// Registration
services.AddSingleton<AsyncActionVisitor<Tender>>(sp =>
{
    var ledger  = sp.GetRequiredService<ILedgerService>();
    var gateway = sp.GetRequiredService<IPaymentGateway>();
    var logger  = sp.GetRequiredService<ILogger<Router>>();

    return AsyncActionVisitor<Tender>.Create()
        .On<Cash>((t, ct) => ledger.ApplyCashAsync(t, ct))
        .On<Card>((t, ct) => gateway.ChargeAsync(t, ct))
        .Default((t, ct) => { logger.LogWarning("Unknown tender {Amount}", t.Amount); return default; })
        .Build();
});

// Usage
public sealed class Router(AsyncActionVisitor<Tender> visitor)
{
    public ValueTask RouteAsync(Tender t, CancellationToken ct) => visitor.VisitAsync(t, ct);
}
```

Notes
- Built visitors are immutable and thread‑safe; builders are not thread‑safe.
- For multi‑tenant rules, build per‑tenant visitors at composition time and select by tenant ID.

---

## Testing

Pattern: parallelize a mixed set of nodes and assert side‑effect counters.

```csharp
var counters = new int[3]; // add, number, default
var v = AsyncActionVisitor<Node>.Create()
    .On<Add>((_, _) => { Interlocked.Increment(ref counters[0]); return default; })
    .On<Number>((_, _) => { Interlocked.Increment(ref counters[1]); return default; })
    .Default((_, _) => { Interlocked.Increment(ref counters[2]); return default; })
    .Build();

await v.VisitAsync(new Add(...));
```

Reference tests: `test/PatternKit.Tests/Behavioral/AsyncVisitorTests.cs:69`.

---

## See Also

- `AsyncVisitor<TBase, TResult>` — async result variant
- `ActionVisitor<TBase>` — synchronous actions
- Troubleshooting — `docs/patterns/behavioral/visitor/troubleshooting.md:1`
 - Examples: `docs/examples/message-router-visitor.md:1`, `docs/examples/event-processor-visitor.md:1`
