# Singleton

A fluent, low-overhead, thread-safe Singleton for .NET. Configure a factory and optional one-time init mutations; choose lazy (default) or eager creation; then access Instance.

- Singleton<T>
  - Create(factory)
  - Init(action) — optional, composable
  - Eager() — optional; creates at Build()
  - Build() — immutable wrapper exposing Instance

---

## TL;DR

```csharp
using PatternKit.Creational.Singleton;

sealed class Cache { public int Warmed; }

var cache = Singleton<Cache>
    .Create(static () => new Cache())
    .Init(static c => c.Warmed++)  // run once, when instance is created
    .Build();                      // lazy by default

var a = cache.Instance;  // created here
var b = cache.Instance;  // same instance
ReferenceEquals(a, b);   // true
```

Eager creation:

```csharp
var s = Singleton<Cache>
    .Create(static () => new Cache())
    .Init(static c => c.Warmed++)
    .Eager()   // create during Build()
    .Build();
```

---

## API at a glance

```csharp
public sealed class Singleton<T>
{
    public delegate T Factory();

    public static Builder Create(Factory factory);

    public T Instance { get; }  // lazy (default) or eager per builder

    public sealed class Builder
    {
        public Builder Init(Action<T> initializer);  // one-time; composed in order
        public Builder Eager();                      // create at Build()
        public Singleton<T> Build();                // returns the wrapper
    }
}
```

### Semantics

- Lazy by default: instance is created on first read of Instance.
- Eager: calling Eager() creates the instance at Build().
- Init runs exactly once, at creation time (lazy or eager). Multiple Init calls compose, preserving order.
- Thread-safe: double-checked lock with Volatile reads/writes; only one factory invocation occurs.
- Builder reuse: each Build() call returns an independent singleton wrapper with its own lifetime.

---

## Patterns and usage

- Global services with explicit initialization that must remain cheap on the hot path.
- Factory + Init keeps construction logic separate from one-time setup (e.g., prefetch, metrics labels).

```csharp
var metrics = Singleton<HttpClient>
    .Create(static () => new HttpClient())
    .Init(static c => c.Timeout = TimeSpan.FromSeconds(10))
    .Build();

var client = metrics.Instance; // same client each time
```

---

## Tips

- Prefer static lambdas / method groups for the factory and init steps to avoid captures.
- Keep Init side effects idempotent; it runs once, but composability is easier when safe to re-run in tests.
- If construction can fail, let it throw; subsequent Instance reads will see the same exception until a successful creation completes.

---

## See also

- [Creational.Factory](../factory/factory.md) — keyed creators for many instances.
- [Creational.Builder.MutableBuilder](../builder/mutablebuilder.md) — configure and validate mutable object graphs.

