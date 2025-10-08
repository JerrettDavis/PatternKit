using PatternKit.Structural.Flyweight;

namespace PatternKit.Examples.FlyweightDemo;

/// <summary>
/// Demonstrations for the Flyweight pattern: glyph layout & style sharing.
/// </summary>
public static class FlyweightDemo
{
    /// <summary>
    /// Immutable intrinsic glyph data (shared). Width is a trivial heuristic.
    /// </summary>
    public sealed record Glyph(char Char, int Width)
    {
        public static readonly Glyph Space = new(' ', 3);
    }

    private static Flyweight<char, Glyph> CreateGlyphs()
        => Flyweight<char, Glyph>.Create()
            .Preload(' ', Glyph.Space)
            .WithFactory(c => c == ' ' ? Glyph.Space : new Glyph(c, InferWidth(c)))
            .Build();

    private static int InferWidth(char c)
        => c switch
        {
            'W' or 'M' => 9,
            'I' or 'l' or 'i' => 4,
            _ => 6
        };

    /// <summary>
    /// Renders a sentence by computing cumulative X positions using shared glyph objects.
    /// Extrinsic state: position (x). Intrinsic state: glyph metrics.
    /// </summary>
    public static IReadOnlyList<(Glyph glyph, int x)> RenderSentence(string text)
    {
        var glyphs = CreateGlyphs();
        var list = new List<(Glyph glyph, int x)>(text.Length);
        var x = 0;
        foreach (var ch in text)
        {
            var g = glyphs.Get(ch);
            list.Add((g, x));
            x += g.Width; // advance by intrinsic width
        }
        return list;
    }

    /// <summary>
    /// Provides statistics about reuse for a given sentence (how many unique vs total glyph instances).
    /// </summary>
    public static (int total, int unique, double reuseRatio) AnalyzeReuse(string text)
    {
        var layout = RenderSentence(text);
        var unique = layout.Select(t => t.glyph).Distinct().Count();
        var total = layout.Count;
        var reuseRatio = total == 0 ? 1d : (double)unique / total; // lower ratio => more reuse
        return (total, unique, reuseRatio);
    }

    /// <summary>
    /// Case-insensitive style flyweight demonstrating custom comparer.
    /// </summary>
    public static (Style a, Style b, bool same) DemonstrateCaseInsensitiveStyles(string aName = "header", string bName = "HEADER")
    {
        var styles = Flyweight<string, Style>.Create()
            .WithComparer(StringComparer.OrdinalIgnoreCase)
            .WithFactory(name => new Style(name.ToUpperInvariant()))
            .Build();

        var a = styles.Get(aName);
        var b = styles.Get(bName);
        return (a, b, ReferenceEquals(a, b));
    }

    /// <summary>
    /// Simple intrinsic style object (shared).
    /// </summary>
    public sealed record Style(string Name);
}

