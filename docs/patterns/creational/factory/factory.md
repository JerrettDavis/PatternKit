# Factory\<TKey, TOut> and Factory\<TKey, TIn, TOut>

A tiny, immutable, low-overhead factory you configure once, then use everywhere. Map a key to a creator delegate and optionally set a default. Two shapes:

- Factory<TKey, TOut> — creators are parameterless constructors or delegates.
- Factory<TKey, TIn, TOut> — creators take an input context by in TIn.

Use it when you want a fluent, allocation-light registry from keys (string/enum/etc.) to constructors.

---

## TL;DR (out-only)

```csharp
using PatternKit.Creational.Factory;

var mime = Factory<string, string>
    .Create(StringComparer.OrdinalIgnoreCase)
    .Map("json", () => "application/json")
    .Map("html", () => "text/html; charset=utf-8")
    .Default(() => "application/octet-stream")
    .Build();

mime.Create("JSON"); // application/json
mime.Create("svg");  // application/octet-stream (default)
```

## TL;DR (with input)

```csharp
using PatternKit.Creational.Factory;

var math = Factory<string, int, int>
    .Create()
    .Map("double", static (in int x) => x * 2)
    .Map("square", static (in int x) => x * x)
    .Default(static (in int x) => x) // identity
    .Build();

math.Create("double", 5); // 10
math.Create("noop", 7);   // 7 (default)
```

---

## Why this factory

- Immutable after Build() — safe for concurrency, predictable snapshots.
- Low noise — tiny fluent API: Map, Default, Build.
- Fast lookups — single dictionary read, no LINQ, no reflection.
- Flexible keys — bring your own IEqualityComparer (e.g., OrdinalIgnoreCase).

---

## API at a glance

```csharp
// Out-only
public sealed class Factory<TKey, TOut>
{
    public delegate TOut Creator();

    public static Builder Create(IEqualityComparer<TKey>? comparer = null);
    public TOut Create(TKey key);                 // throws if missing and no Default
    public bool TryCreate(TKey key, out TOut v);  // false only if no mapping and no Default

    public sealed class Builder
    {
        public Builder Map(TKey key, Creator creator);  // last mapping wins
        public Builder Default(Creator creator);         // optional
        public Factory<TKey, TOut> Build();
    }
}

// With input
public sealed class Factory<TKey, TIn, TOut>
{
    public delegate TOut Creator(in TIn input);

    public static Builder Create(IEqualityComparer<TKey>? comparer = null);
    public TOut Create(TKey key, in TIn input);
    public bool TryCreate(TKey key, in TIn input, out TOut v);

    public sealed class Builder
    {
        public Builder Map(TKey key, Creator creator);
        public Builder Default(Creator creator);
        public Factory<TKey, TIn, TOut> Build();
    }
}
```

### Semantics

- Last mapping wins: Map(key, ...) replaces the previous mapping for that key.
- Default is optional: Create throws if no mapping and no Default. TryCreate returns false.
- Snapshots: each Build() captures a read-only copy; later Map/Default calls don’t mutate prior factories.
- in TIn avoids copies for struct inputs on the hot path.

---

## Patterns and usage

- Text/content negotiation: string key → serializer/formatter.
- Plugin registry: enum key → adapter/strategy creator.
- Parsing with context: format → (in ReadOnlySpan<char> s) => T.

```csharp
public enum ShapeKind { Circle, Square }

var shapes = Factory<ShapeKind, double, string>
    .Create()
    .Map(ShapeKind.Circle, static (in double r) => $"Circle area={Math.PI * r * r:0.##}")
    .Map(ShapeKind.Square, static (in double s) => $"Square area={s * s:0.##}")
    .Default(static (in double _) => "unknown")
    .Build();

var s1 = shapes.Create(ShapeKind.Circle, 2.0); // "Circle area=12.57"
```

---

## Gotchas

- Missing default: If you call Create with an unknown key and never configured Default, you’ll get InvalidOperationException. Prefer TryCreate when probing.
- Key comparers: Use the Create(comparer) overload to control case sensitivity and cultural rules.
- Keep creators pure: Return new instances or value results; avoid hidden shared mutable state unless intentional.

---

## See also

- Creational.Builder.BranchBuilder — first-match routers and strategy builders.
- Behavioral.Strategy.* — complementary selection patterns when you need predicate-driven routing rather than keyed factories.

