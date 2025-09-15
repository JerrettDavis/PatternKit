# ChainBuilder\<T>

A tiny, allocation-light builder that collects items **in order** and then **projects** them into any product type. It’s the backbone for “append things, then freeze into an immutable structure” scenarios (e.g., composing pipelines).

---

## Mental model

* **Append-only order.** `Add` pushes to the end; order is preserved.
* **Conditional append.** `AddIf(cond, item)` only appends when `cond` is `true`.
* **Snapshot on build.** `Build(projector)` copies items to a fresh array and passes it to your projector. Subsequent `Add` calls don’t mutate previously built products.

---

## API at a glance

```csharp
var b = ChainBuilder<T>.Create();

b.Add(T item);               // append item
b.AddIf(bool condition, T);  // append only when condition is true

TProduct product = b.Build(items => /* construct product from T[] */);
```

### Threading & immutability

* Builders are **not** thread-safe.
* `Build` hands you a **new array snapshot** each time—treat it as immutable in your product.

---

## Minimal examples

### 1) Make a simple CSV projector

```csharp
var csv = ChainBuilder<int>.Create()
    .Add(1)
    .Add(2)
    .AddIf(false, 99) // ignored
    .Build(items => string.Join(",", items));
// "1,2"
```

### 2) Build and reuse with snapshots

```csharp
var b = ChainBuilder<int>.Create().Add(1).Add(2);

var first  = b.Build(items => items.Length); // 2
b.Add(3);
var second = b.Build(items => items.Length); // 3

// 'first' used the earlier snapshot; wasn't mutated by Add(3)
```

### 3) Compose a middleware delegate (handlers list → single runner)

```csharp
// Handler is (in TCtx ctx, Next next) => void
public delegate void Handler<TCtx>(in TCtx ctx, Action<in TCtx> next);

var handlers = ChainBuilder<Handler<int>>.Create()
    .Add((in int x, next) => { Console.Write("[A]"); next(in x); })
    .Add((in int x, next) => { Console.Write("[B]"); next(in x); })
    .Build(items =>
    {
        // compose from end to start
        Action<in int> next = static (in _) => { };
        for (var i = items.Length - 1; i >= 0; i--)
        {
            var h = items[i];
            var prev = next;
            next = (in int c) => h(in c, prev);
        }
        return next;
    });

handlers(in 0); // prints [A][B]
```

---

## Usage patterns & tips

* **Feature-flagged registration:** wrap `AddIf(flag, item)` to keep builder clutter-free.
* **Multiple products from one builder:** call `Build` multiple times with different projectors (e.g., build a runner and a debug view).
* **Low overhead:** Lists while building; exactly one `ToArray()` per `Build`.

---

## Gotchas

* **No removal/reorder.** It’s purposefully simple—append in the order you want to execute.
* **Projector owns semantics.** `ChainBuilder<T>` doesn’t interpret items; your projector decides what they mean.

---

## Reference (public API)

```csharp
public sealed class ChainBuilder<T>
{
    public static ChainBuilder<T> Create();

    public ChainBuilder<T> Add(T item);
    public ChainBuilder<T> AddIf(bool condition, T item);

    public TProduct Build<TProduct>(Func<T[], TProduct> projector);
}
```

---

## See also

* `BranchBuilder<TPred,THandler>` – collect predicate/handler pairs + optional default, then project.
* `Behavioral.Chain.ActionChain` / `Behavioral.Chain.ResultChain` – real pipelines built atop these patterns.
* `Behavioral.Strategy.TryStrategy` – uses `ChainBuilder<TryHandler>` for first-success execution.
