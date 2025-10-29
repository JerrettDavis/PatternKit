# ActionVisitor

Side‑effecting visitor that maps runtime types to actions, evaluated with first‑match‑wins semantics.

---

## What It Is

- Type‑to‑action mapping: `On<T>(Action<T>)` with optional `.Default(...)`.
- First matching action executes; others are skipped.
- Non‑throwing variant: `TryVisit(in TBase)` returns `false` when no action/default matched.

---

## TL;DR Example

```csharp
var v = ActionVisitor<Tender>
    .Create()
    .On<Cash>(t => CashHandler(t))
    .On<Card>(t => CardHandler(t))
    .Default(t => LogUnhandled(t))
    .Build();

v.Visit(tender); // throws if no match and no default
```

See `src/PatternKit.Examples/VisitorDemo/VisitorDemo.cs:48` for a complete routing demo.

---

## API

- `void Visit(in TBase node)` — runs first matching action or the default; throws if neither applies.
- `bool TryVisit(in TBase node)` — returns `false` when nothing matches and no default exists.
- `Builder.On<T>(Action<T>)` — registers a type‑specific action; evaluation order is registration order.
- `Builder.Default(ActionHandler)` — registers a fallback for unknown types.

---

## Operational Guidance

- Prefer idempotent and fast actions; guard side effects (I/O, retries) at the edges.
- Put the most specific types first to avoid base‑type shadowing.
- Share built visitors as singletons; builders are not thread‑safe.
- Use `.Default(...)` to log and continue on unknown types in production.

---

## Testing

Reference tests: `test/PatternKit.Tests/Behavioral/VisitorTests.cs:49`

```csharp
var counters = new int[3];
var v = ActionVisitor<Node>.Create()
    .On<Add>(_ => Interlocked.Increment(ref counters[0]))
    .On<Number>(_ => Interlocked.Increment(ref counters[1]))
    .Default(_ => Interlocked.Increment(ref counters[2]))
    .Build();

v.Visit(new Add(...));
v.Visit(new Number(...));
v.Visit(new Neg(...)); // default
```

---

## See Also

- `Visitor<TBase, TResult>` — result‑producing variant
- `AsyncActionVisitor<TBase>` — asynchronous actions
