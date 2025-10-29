# Visitor (Fluent)

Visitor separates operations from the objects they operate on. PatternKit provides fluent, type‑safe visitors that dispatch by runtime type and either return a value (result visitor) or perform side effects (action visitor). Use it to add operations like formatting, routing, validation, and projection without modifying your model types.

---

## What It Is

- Type‑based dispatch using a fluent builder: `On<TSub>(...)` with optional `.Default(...)`.
- First‑match‑wins evaluation. Put specific types before base types.
- Immutable and thread‑safe after `Build()`; the builder itself is not thread‑safe.
- Non‑intrusive: your domain types do not need to implement `Accept(...)` (no classic double‑dispatch required).

> Variants: Result `Visitor<TBase, TResult>`, side‑effecting `ActionVisitor<TBase>`, and async `AsyncVisitor`/`AsyncActionVisitor` using `ValueTask` + `CancellationToken`.

---

## When To Use

- You have a stable type hierarchy (e.g., AST nodes, payments, UI elements) but frequently add new operations.
- You want to avoid modifying domain classes for every new behavior (formatters, validators, routers).
- You want a clear, centralized, discoverable composition point for type‑specific behavior.

Avoid if:
- You only have a handful of operations and the types themselves are the best home for the logic (simple virtual methods may suffice).
- Pattern matching with `switch` expressions is simpler and sufficient (no need for reusable composition).

---

## TL;DR Example (Result Visitor)

```csharp
var v = Visitor<Node, string>
    .Create()
    .On<Add>(_ => "+")
    .On<Number>(n => $"#{n.Value}")
    .Default(_ => "?")
    .Build();

var a = v.Visit(new Add(new Number(1), new Number(2))); // "+"
var b = v.Visit(new Number(7));                          // "#7"
```

If you omit `.Default(...)` and no handler matches, `Visit` throws `InvalidOperationException`. Use `TryVisit` for a non‑throwing path.

---

## TL;DR Example (Action Visitor)

```csharp
var v = ActionVisitor<Node>
    .Create()
    .On<Add>(_ => Log("add"))
    .On<Number>(_ => Count++)
    .Default(_ => Skip())
    .Build();

v.Visit(new Add(new Number(1), new Number(2)));
```

See also `AsyncVisitor<TBase, TResult>` and `AsyncActionVisitor<TBase>` for asynchronous variants.

---

## API Shape

```csharp
var resultVisitor = Visitor<TBase, TResult>.Create()
    .On<Concrete>(static x => /* TResult */)
    .Default(static (in TBase x) => /* TResult */) // optional
    .Build();

TResult r = resultVisitor.Visit(in node);        // throws if no match and no default
bool ok = resultVisitor.TryVisit(in node, out r); // non‑throwing
```

Key points
- Handlers and predicates use `in` parameters to avoid copying large structs.
- Registration order is evaluation order; the first matching registration runs.
- A `.Default(...)` provides a fallback when nothing matches.

---

## Ordering And Specificity

- Register more specific types before base types to prevent shadowing.
- Group related registrations for readability (e.g., all numeric nodes, then structural nodes).
- For very large hierarchies, consider composing multiple visitors for locality.

---

## Defaults, Errors, And TryVisit

- No default + no match: `Visit` throws `InvalidOperationException`.
- `TryVisit(in, out)` always avoids throwing on no‑match: it returns `false` and sets the `out` parameter to `default`.
- Prefer a `.Default(...)` for resilience and observability in production code; log and continue.

---

## Performance And Thread Safety

- Dispatch is a tight `for` loop over an array of predicates/handlers. No reflection in the hot path.
- Built visitors are immutable and safe to share across threads. Builders are not thread‑safe.
- For hot paths with many registrations, keep most frequent types early and consider splitting by module/domain.

---

## Composition & DI

Register built visitors as singletons; capture collaborators in closures or expose a factory method.

```csharp
// Registration
services.AddSingleton<Visitor<Tender, string>>(sp =>
{
    var settings = sp.GetRequiredService<IReceiptSettings>();
    return Visitor<Tender, string>
        .Create()
        .On<Cash>(t => $"Cash          {t.Value,8:C}")
        .On<Card>(t => $"{t.Brand} ****{t.Last4,4} {t.Value,8:C}")
        .On<GiftCard>(t => $"GiftCard {t.Code,-8} {t.Value,8:C}")
        .On<StoreCredit>(t => $"StoreCredit {t.CustomerId,-6} {t.Value,8:C}")
        .Default(t => settings.ShowRaw ? $"Other {t.Amount:C}" : "Other")
        .Build();
});

// Usage
public sealed class ReceiptService(Visitor<Tender, string> renderer)
{
    public string LineFor(Tender t) => renderer.Visit(t);
}
```

Notes
- Built visitors are immutable and thread‑safe; the builder is not.
- For multi‑tenant rules, compose per‑tenant visitors at startup and select by tenant key.

---

## End‑To‑End Example (POS)

PatternKit ships a complete example that renders receipt lines and routes tenders by runtime type.

- Example code: `src/PatternKit.Examples/VisitorDemo/VisitorDemo.cs:15`
- Walkthrough: `docs/examples/pos-visitor-routing.md:1`

---

## Real‑World Recipes

- API error mapping: translate exceptions to `ProblemDetails` or typed results.
  - Example: `docs/examples/api-exception-mapping-visitor.md:1`
- Event processing: route domain events to handlers and orchestrate side effects.
  - Example: `docs/examples/event-processor-visitor.md:1`
- Message routing in workers: dispatch queue messages to specialized processors with cancellation.
  - Example: `docs/examples/message-router-visitor.md:1`

These patterns keep type‑specific behavior in one place, are easy to test, and wire cleanly into DI.

---

## Testing (TinyBDD style)

```csharp
[Scenario("Result visitor dispatch and default")]
[Fact]
public Task ResultVisitor_Dispatch_And_Default()
    => Given("a result visitor", () =>
        Visitor<Node, string>.Create()
            .On<Add>(_ => "+")
            .On<Number>(n => $"#{n.Value}")
            .Default(_ => "?")
            .Build())
       .When("visit three nodes", v => (
           a: v.Visit(new Add(new Number(1), new Number(2))),
           b: v.Visit(new Number(7)),
           c: v.Visit(new Neg(new Number(1))) // default
       ))
       .Then("Add -> +", r => r.a == "+")
       .And("Number -> #7", r => r.b == "#7")
       .And("Neg -> ?", r => r.c == "?")
       .AssertPassed();
```

Reference tests: `test/PatternKit.Tests/Behavioral/VisitorTests.cs:16`

---

## Design Notes

- Non‑intrusive visitor: you don’t need the classic `Accept(Visitor)` on domain types.
- Zero‑allocation dispatch; strongly‑typed handlers; no hidden boxing for value types when using `in`.
- Built on `BranchBuilder` for composable construction.

---

## Gotchas

- Missing `.Default(...)` + no match throws. Prefer `TryVisit` for defensive paths.
- Wrong registration order can shadow base types. Put most specific first.
- Actions should be idempotent; guard external side effects.

---

## See Also

- `ActionVisitor<TBase>` — side‑effects only; no return
- `AsyncVisitor<TBase, TResult>` — async result
- `AsyncActionVisitor<TBase>` — async actions
- Examples — `docs/examples/pos-visitor-routing.md:1`
- Alternatives and comparisons — `docs/patterns/behavioral/visitor/alternatives.md:1`
