using PatternKit.Generators.Flyweight;

namespace PatternKit.Generators.Tests;

public class FlyweightGeneratorTests
{
    [Fact]
    public void GeneratesFlyweightCacheWithLru()
    {
        const string source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string), CacheTypeName = "GlyphCache", Capacity = 2, Eviction = FlyweightEviction.Lru)]
            public readonly partial record struct Glyph(char Value)
            {
                [FlyweightFactory]
                private static Glyph Create(string key) => new(key[0]);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesFlyweightCacheWithLru));
        var gen = new FlyweightGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "Glyph.Flyweight.g.cs").SourceText.ToString();
        Assert.Contains("public sealed partial class GlyphCache", generated);
        Assert.Contains("public global::TestNamespace.Glyph Get(string key)", generated);
        Assert.Contains("public bool TryGet(string key, out global::TestNamespace.Glyph value)", generated);
        Assert.Contains("private void Trim()", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ReportsMissingFactory()
    {
        const string source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string))]
            public readonly partial record struct Glyph(char Value);
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsMissingFactory));
        var gen = new FlyweightGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKFLY002");
    }

    [Fact]
    public void ReportsInvalidLruConfiguration()
    {
        const string source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string), Eviction = FlyweightEviction.Lru)]
            public readonly partial record struct Glyph(char Value)
            {
                [FlyweightFactory]
                private static Glyph Create(string key) => new(key[0]);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsInvalidLruConfiguration));
        var gen = new FlyweightGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKFLY006");
    }
}
