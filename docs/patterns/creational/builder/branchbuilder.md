# BranchBuilder\<TPred, THandler>

A tiny, reusable builder for collecting **predicate/handler pairs** (plus an optional **default**) and projecting them into any concrete “strategy-like” product. It’s the core used by `ActionStrategy`, `Strategy`, and `AsyncStrategy`.

---

## Why it exists

Lots of “first-match-wins” constructs look the same: you register ordered predicate/handler pairs, optionally set a default, then build an immutable thing. `BranchBuilder` captures that pattern so you can:

* Avoid re-implementing the same plumbing.
* Keep allocations minimal (lists while building, single `ToArray()` on `Build()`).
* Project the collected data into any product type via a **projector** function.

---

## Mental model

* **Registration order matters.** The `i`th predicate corresponds to the `i`th handler.
* **Default is optional.** If you don’t set one, a **fallback** you supply at build time is used, and you get a `hasDefault=false` flag.
* **Build is a snapshot.** Each call copies to arrays; later calls don’t mutate earlier products.

---

## API at a glance

```csharp
var b = BranchBuilder<TPred, THandler>.Create();

b.Add(TPred predicate, THandler handler);   // append a pair (order preserved)
b.Default(THandler handler);                // set/replace default

TProduct product = b.Build(
    fallbackDefault: THandler,              // used if no Default() was configured
    projector: (TPred[] preds,
                THandler[] handlers,
                bool hasDefault,
                THandler @default) => /* construct product */
);
```

### Threading & immutability

* Builders are **not** thread-safe.
* Arrays passed to your projector are **fresh snapshots**. Treat them as immutable in your product.

---

## Minimal examples

### 1) Build a simple classifier (sync)

```csharp
// Shapes
delegate bool Pred(in int x);
delegate string Handler(in int x);

// Predicates/handlers
static bool IsEven(in int x) => (x & 1) == 0;
static bool IsPositive(in int x) => x > 0;
static string HandleEven(in int _) => "even";
static string HandlePositive(in int _) => "pos";
static string Fallback(in int _) => "other";

sealed record Classifier(Pred[] Preds, Handler[] Handlers, bool HasDefault, Handler Default)
{
    public string Execute(in int x)
    {
        for (var i = 0; i < Preds.Length; i++)
            if (Preds[i](in x)) return Handlers[i](in x);
        return Default(in x);
    }
}

var classifier =
    BranchBuilder<Pred, Handler>.Create()
        .Add(IsEven, HandleEven)
        .Add(IsPositive, HandlePositive)
        .Build(fallbackDefault: Fallback,
               projector: (p, h, hasDef, def) => new Classifier(p, h, hasDef, def));

classifier.Execute(2);  // "even"
classifier.Execute(1);  // "pos"
classifier.Execute(-1); // "other" (fallback)
```

### 2) Swap in a real default (not fallback)

```csharp
static string RealDefault(in int _) => "default";

var withRealDefault =
    BranchBuilder<Pred, Handler>.Create()
        .Add(IsEven, HandleEven)
        .Default(RealDefault)
        .Build(Fallback, (p, h, hasDef, def) => new Classifier(p, h, hasDef, def));

// withRealDefault.HasDefault == true; withRealDefault.Default == RealDefault
```

### 3) What the built-in strategies do

All of these are thin wrappers over `BranchBuilder`:

* `ActionStrategy<TIn>` → predicates + **action** handlers (`void`).
* `Strategy<TIn,TOut>` → predicates + **result** handlers (`TOut`).
* `AsyncStrategy<TIn,TOut>` → async predicates/handlers (`ValueTask`).

Each supplies a sensible **fallback default** to `Build(...)` and a projector that constructs the immutable strategy.

---

## Usage patterns & tips

* **Replace defaults:** calling `Default(...)` multiple times replaces the previous one (“last wins”).
* **Conditional registration:** gate calls to `.Add(...)` with your own `if` or feature flags. (The conditional DSL lives in `TryStrategy`; `BranchBuilder` stays simple.)
* **Multiple products from one builder:** you can call `Build(...)` more than once. Each build snapshots current pairs and default.
* **Interop with `in` parameters:** Using `in` in your delegate shapes keeps handlers low-overhead for structs.

---

## Gotchas

* **No validation of shapes.** `TPred`/`THandler` are just types; ensure they’re the right delegates for your projector.
* **Default semantics:** If you never call `Default(...)`, your projector receives `hasDefault=false` and the **fallback** handler as `@default`. Use the flag to distinguish “user configured” vs “library fallback”.

---

## Reference (public API)

```csharp
public sealed class BranchBuilder<TPred, THandler>
{
    public static BranchBuilder<TPred, THandler> Create();

    public BranchBuilder<TPred, THandler> Add(TPred predicate, THandler handler);
    public BranchBuilder<TPred, THandler> Default(THandler handler);

    public TProduct Build<TProduct>(
        THandler fallbackDefault,
        Func<TPred[], THandler[], bool, THandler, TProduct> projector);
}
```

---

## See also

* [ActionStrategy](../../behavioral/strategy/actionstrategy.md) – first-match actions.
* [Strategy](../../behavioral/strategy/strategy.md) – first-match handlers that return values.
* [AsyncStrategy](../../behavioral/strategy/asyncstrategy.md) – async first-match strategy.
* [ActionChain](../../behavioral/chain/actionchain.md) / [ResultChain](../../behavioral/chain/resultchain.md) – chain style (middleware) alternatives.
