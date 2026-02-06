# Flyweight Generator Demo

Demonstrates how the Flyweight Pattern generator creates a shared-instance cache for glyph rendering. The `[Flyweight]` attribute and `[FlyweightFactory]` method produce a type-safe cache with Get, TryGet, and Clear operations.

## Goal

Share immutable `Glyph` instances across a text rendering system so that each character is represented by exactly one object, regardless of how many times it appears in the text.

## Key idea

Mark the value type with `[Flyweight(typeof(char))]` and a static factory with `[FlyweightFactory]`. The generator produces a nested `GlyphCache` class. Repeated `Get` calls for the same key return the **same instance**.

## Code snippet

```csharp
using PatternKit.Generators.Flyweight;

[Flyweight(typeof(char), Capacity = 256, Threading = FlyweightThreadingPolicy.Locking)]
public partial class Glyph
{
    public char Character { get; }
    public int Width { get; }
    public int Height { get; }

    private Glyph(char character, int width, int height)
    {
        Character = character;
        Width = width;
        Height = height;
    }

    [FlyweightFactory]
    public static Glyph Create(char key)
    {
        var width = char.IsUpper(key) ? 12 : 8;
        return new Glyph(key, width, 16);
    }
}

// Usage:
var a1 = Glyph.GlyphCache.Get('A');  // Creates new instance
var a2 = Glyph.GlyphCache.Get('A');  // Returns cached instance
// ReferenceEquals(a1, a2) == true

// Render a word -- all duplicate characters share the same Glyph
foreach (var ch in "Hello")
{
    var glyph = Glyph.GlyphCache.Get(ch);
    Render(glyph);
}
// The two 'l' characters use the same Glyph instance
```

## Mental model

```
Glyph.GlyphCache.Get('A')
    |
    +-- Cache miss? --> Glyph.Create('A') --> store & return
    +-- Cache hit?  --> return existing instance
```

- `Get(key)` -- Always returns a value (creates if needed)
- `TryGet(key, out value)` -- Returns `false` if not cached (does not create)
- `Clear()` -- Evicts all cached instances
- `Count` -- Number of currently cached entries

## Test references

- `FlyweightGeneratorDemoTests.Run_Returns_Glyph_Sharing_Results` -- validates demo output
- `FlyweightGeneratorDemoTests.Cache_Returns_Same_Instance` -- verifies identity equality
- `FlyweightGeneratorDemoTests.TryGet_Returns_False_For_Uncached` -- TryGet semantics
- `FlyweightGeneratorDemoTests.Clear_Empties_Cache` -- cache eviction

## See Also

- [Flyweight Generator Reference](../generators/flyweight.md)
- [Flyweight Glyph Cache Example](flyweight-glyph-cache.md)
