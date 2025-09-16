# Prototype\<T> and Prototype\<TKey, T>

A fluent, low-overhead Prototype that clones a configured instance and applies optional mutations, plus a keyed registry for multiple prototype families.

- Prototype<T> — single source + cloner + default mutations; Create() and Create(mutate).
- Prototype<TKey, T> — registry mapping key → (source, cloner, mutations); Create/TryCreate with optional per-call mutate; comparer support for keys.

Use it when you want to “copy and tweak” new instances quickly without large object graphs or reflection.

---

## TL;DR (single)

```csharp
using PatternKit.Creational.Prototype;

public sealed class Widget { public string Name { get; set; } public int Size { get; set; } }
static Widget Clone(in Widget w) => new() { Name = w.Name, Size = w.Size };

var proto = Prototype<Widget>
    .Create(new Widget { Name = "base", Size = 1 }, Clone)
    .With(w => w.Size++)               // default mutation
    .Build();

var a = proto.Create();                // Name=base, Size=2
var b = proto.Create(w => w.Size += 8);// Name=base, Size=10
```

## TL;DR (registry)

```csharp
using PatternKit.Creational.Prototype;

enum Kind { Circle, Square }

public sealed class Shape { public string Name { get; set; } public double A { get; set; } }
static Shape Clone(in Shape s) => new() { Name = s.Name, A = s.A };

var shapes = Prototype<Kind, Shape>
    .Create()
    .Map(Kind.Circle, new Shape { Name = "circle", A = 2 }, Clone)
    .Map(Kind.Square, new Shape { Name = "square", A = 3 }, Clone)
    .Default(new Shape { Name = "unknown", A = 0 }, Clone)
    .Build();

var c = shapes.Create(Kind.Circle);                // circle, A=2
var s = shapes.Create(Kind.Square, sh => sh.A++);  // square, A=4 (mutated)
```

---

## API at a glance

```csharp
// Single
public sealed class Prototype<T>
{
    public delegate T Cloner(in T source);

    public static Builder Create(T source, Cloner cloner);

    public sealed class Builder
    {
        public Builder With(Action<T> mutate);  // append default mutations (combined)
        public Prototype<T> Build();
    }

    public T Create();                // clone + default mutations
    public T Create(Action<T> mutate);// clone + default + per-call mutation
}

// Registry
public sealed class Prototype<TKey, T> where TKey : notnull
{
    public delegate T Cloner(in T source);

    public static Builder Create(IEqualityComparer<TKey>? comparer = null);

    public sealed class Builder
    {
        public Builder Map(TKey key, T source, Cloner cloner);  // last wins
        public Builder Mutate(TKey key, Action<T> mutate);       // append family mutations
        public Builder Default(T source, Cloner cloner);         // optional default
        public Builder DefaultMutate(Action<T> mutate);          // append default mutations
        public Prototype<TKey, T> Build();
    }

    public T Create(TKey key);                    // throws if missing and no default
    public T Create(TKey key, Action<T> mutate);  // per-call mutate
    public bool TryCreate(TKey key, out T value); // false only if no mapping and no default
}
```

### Semantics

- Cloning uses your Cloner delegate (no reflection); prefer method groups/static lambdas.
- Default mutations are composed in order; per-call mutate runs after defaults.
- Registry: last Map wins; Mutate appends; Build snapshots an immutable map; comparer controls key semantics.
- Create throws when no mapping and no default; TryCreate returns false in that case.

---

## Tips

- Keep Clone simple and pure; avoid sharing mutable state between clones.
- For structs or hot paths, keep fields small and prefer in parameters to avoid copies.
- With string keys, pass StringComparer.OrdinalIgnoreCase to Create for case-insensitive lookups.

---

## See also

- [Creational.Factory](../factory/factory.md) — key → creator registry (no source object).
- [Creational.Builder.Composer](../builder/composer.md) — functional composition when you need transforms + validations.

