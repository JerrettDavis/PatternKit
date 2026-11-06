# Flyweight Generator

The Flyweight generator emits a typed cache for a partial value type. Generated code is reflection-free and has no runtime dependency on PatternKit.

## Usage

```csharp
using PatternKit.Generators.Flyweight;

[Flyweight(typeof(string), CacheTypeName = "GlyphCache", Capacity = 10_000, Eviction = FlyweightEviction.Lru)]
public readonly partial record struct Glyph(char Value)
{
    [FlyweightFactory]
    private static Glyph Create(string key) => new(key[0]);
}
```

The generated cache exposes `Get`, `TryGet`, and `Clear`. Cache hits return the existing value; misses invoke the annotated factory under a lock so the factory is called once per key.

## Eviction

`FlyweightEviction.None` creates an unbounded cache. `FlyweightEviction.Lru` requires `Capacity > 0` and evicts the least recently used key deterministically.

## Diagnostics

- `PKFLY001`: flyweight type must be partial.
- `PKFLY002`: no factory method found.
- `PKFLY003`: multiple factory methods found.
- `PKFLY004`: factory signature invalid.
- `PKFLY005`: generated cache type name conflicts.
- `PKFLY006`: invalid eviction configuration.
