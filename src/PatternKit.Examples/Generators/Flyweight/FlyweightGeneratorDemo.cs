using PatternKit.Generators.Flyweight;

namespace PatternKit.Examples.Generators.Flyweight;

/// <summary>
/// A glyph representing a single character with font metadata.
/// The [Flyweight] attribute generates a GlyphCache class that ensures
/// only one Glyph instance exists per character key.
/// </summary>
[Flyweight(typeof(char), Capacity = 256, Threading = FlyweightThreadingPolicy.Locking)]
public partial class Glyph
{
    /// <summary>The character this glyph represents.</summary>
    public char Character { get; }

    /// <summary>The width of the glyph in pixels.</summary>
    public int Width { get; }

    /// <summary>The height of the glyph in pixels.</summary>
    public int Height { get; }

    /// <summary>A unique identifier for tracking instance sharing.</summary>
    public int InstanceId { get; }

    private static int _nextId;

    private Glyph(char character, int width, int height)
    {
        Character = character;
        Width = width;
        Height = height;
        InstanceId = _nextId++;
    }

    /// <summary>
    /// Factory method that creates a new Glyph for a given character.
    /// This is called by the generated cache only when the character
    /// is not already cached.
    /// </summary>
    [FlyweightFactory]
    public static Glyph Create(char key)
    {
        // Simulate computing glyph metrics from a font
        var width = char.IsUpper(key) ? 12 : 8;
        var height = 16;
        return new Glyph(key, width, height);
    }

    /// <inheritdoc />
    public override string ToString()
        => $"Glyph('{Character}', {Width}x{Height}, id={InstanceId})";
}

/// <summary>
/// Demonstrates the Flyweight pattern source generator with a glyph rendering scenario.
/// Shows how the generator creates a cache class with Get, TryGet, and Clear methods
/// to share immutable glyph instances across a text rendering system.
/// </summary>
public static class FlyweightGeneratorDemo
{
    /// <summary>
    /// Runs a demonstration of the flyweight cache for glyph sharing.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();

        // Clear any prior state
        Glyph.GlyphCache.Clear();

        // First access creates a new glyph
        var a1 = Glyph.GlyphCache.Get('A');
        log.Add($"Get 'A': {a1}");

        // Second access returns the same instance
        var a2 = Glyph.GlyphCache.Get('A');
        log.Add($"Get 'A' again: {a2}");
        log.Add($"Same instance: {ReferenceEquals(a1, a2)}");

        // Different character creates a different instance
        var b = Glyph.GlyphCache.Get('b');
        log.Add($"Get 'b': {b}");

        // TryGet returns true for cached characters
        var found = Glyph.GlyphCache.TryGet('A', out var cached);
        log.Add($"TryGet 'A': found={found}, instance={cached}");

        // TryGet returns false for uncached characters
        var notFound = Glyph.GlyphCache.TryGet('z', out _);
        log.Add($"TryGet 'z' (not cached): found={notFound}");

        // Render a word using shared glyphs
        var word = "Hello";
        log.Add($"Rendering '{word}':");
        foreach (var ch in word)
        {
            var glyph = Glyph.GlyphCache.Get(ch);
            log.Add($"  {glyph}");
        }

        // Show cache count
        log.Add($"Cache count: {Glyph.GlyphCache.Count}");

        // The two 'l' glyphs should be the same instance
        var l1 = Glyph.GlyphCache.Get('l');
        var l2 = Glyph.GlyphCache.Get('l');
        log.Add($"Both 'l' glyphs same instance: {ReferenceEquals(l1, l2)}");

        // Clear the cache
        Glyph.GlyphCache.Clear();
        log.Add($"After Clear, cache count: {Glyph.GlyphCache.Count}");

        return log;
    }
}
