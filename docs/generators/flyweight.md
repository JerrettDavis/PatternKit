# Flyweight Pattern Generator

The Flyweight Pattern Generator automatically creates cache classes for sharing immutable instances. It eliminates boilerplate code for instance pooling while providing thread-safe access, configurable eviction policies, and bounded capacity.

## Overview

The generator produces:

- **Cache class** nested inside the annotated type with Get, TryGet, Clear, and Count
- **Thread-safe access** via configurable threading policies (single-threaded, locking, concurrent)
- **Bounded caching** with optional capacity limits
- **LRU eviction** for capacity-constrained caches
- **Zero runtime overhead** through source generation

## Quick Start

### 1. Define Your Flyweight Type

Mark your type with `[Flyweight]` and provide a factory method:

```csharp
using PatternKit.Generators.Flyweight;

[Flyweight(typeof(string))]
public partial class Glyph
{
    public char Character { get; }
    public int Width { get; }

    private Glyph(char character, int width)
    {
        Character = character;
        Width = width;
    }

    [FlyweightFactory]
    public static Glyph Create(string key)
    {
        return new Glyph(key[0], key[0] >= 'A' && key[0] <= 'Z' ? 12 : 8);
    }
}
```

### 2. Build Your Project

The generator runs during compilation and produces a nested cache class:

```csharp
var glyph = Glyph.GlyphCache.Get("A");  // Creates and caches
var same = Glyph.GlyphCache.Get("A");   // Returns cached instance
// ReferenceEquals(glyph, same) == true
```

### 3. Generated Code

```csharp
partial class Glyph
{
    public static class GlyphCache
    {
        public static Glyph Get(string key) { ... }
        public static bool TryGet(string key, out Glyph value) { ... }
        public static void Clear() { ... }
        public static int Count { get; }
    }
}
```

## Attributes

### `[Flyweight(Type keyType)]`

Applied to a partial class or struct. Generates a cache class.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `keyType` | `Type` | *(required)* | Constructor parameter: the cache key type |
| `CacheTypeName` | `string?` | `"{TypeName}Cache"` | Custom name for the generated cache class |
| `Capacity` | `int` | `0` (unbounded) | Maximum entries in the cache |
| `Eviction` | `FlyweightEviction` | `None` | Eviction policy when capacity is reached |
| `Threading` | `FlyweightThreadingPolicy` | `Locking` | Thread-safety model |
| `GenerateTryGet` | `bool` | `true` | Whether to generate the TryGet method |

### `[FlyweightFactory]`

Applied to a static method that creates flyweight instances. Must have the signature `static TValue Create(TKey key)`.

### Enums

**`FlyweightEviction`**
- `None` (0) -- No eviction; new entries bypass cache when full
- `Lru` (1) -- Least Recently Used eviction; requires `Capacity > 0`

**`FlyweightThreadingPolicy`**
- `SingleThreadedFast` (0) -- No synchronization
- `Locking` (1) -- Uses `lock` for thread safety
- `Concurrent` (2) -- Uses `ConcurrentDictionary`

## Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| PKFLY001 | Error | Type marked with `[Flyweight]` must be partial |
| PKFLY002 | Error | No method marked with `[FlyweightFactory]` found |
| PKFLY003 | Error | Multiple methods marked with `[FlyweightFactory]` found |
| PKFLY004 | Error | Factory method has invalid signature (must be static, one parameter, correct return type) |
| PKFLY005 | Error | Cache type name conflicts with an existing member |
| PKFLY006 | Error | LRU eviction requires `Capacity > 0` |

## Examples

### Bounded Cache with LRU Eviction

```csharp
[Flyweight(typeof(int), Capacity = 1000, Eviction = FlyweightEviction.Lru)]
public partial class Color
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    private Color(byte r, byte g, byte b) { R = r; G = g; B = b; }

    [FlyweightFactory]
    public static Color Create(int rgb)
        => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
```

### High-Concurrency Cache

```csharp
[Flyweight(typeof(string), Threading = FlyweightThreadingPolicy.Concurrent)]
public partial class ResourceHandle
{
    public string Path { get; }
    private ResourceHandle(string path) { Path = path; }

    [FlyweightFactory]
    public static ResourceHandle Create(string path) => new(path);
}
```

## Best Practices

- Use flyweights for **immutable** value-like objects that are frequently reused
- Choose `Concurrent` threading for read-heavy, high-contention scenarios
- Set a `Capacity` to prevent unbounded memory growth in long-running applications
- Use `Lru` eviction with `Capacity` for working-set caching patterns
- Keep factory methods pure -- they may be called concurrently

## See Also

- [Flyweight Generator Demo](../examples/flyweight-generator-demo.md)
- [Flyweight Glyph Cache Example](../examples/flyweight-glyph-cache.md)
