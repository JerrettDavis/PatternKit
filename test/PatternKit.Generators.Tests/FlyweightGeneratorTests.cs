using PatternKit.Generators.Flyweight;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class FlyweightGeneratorTests
{
    [Scenario("GeneratesFlyweightCacheWithLru")]
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

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "Glyph.Flyweight.g.cs").SourceText.ToString();
        ScenarioExpect.Contains("public sealed partial class GlyphCache", generated);
        ScenarioExpect.Contains("public global::TestNamespace.Glyph Get(string key)", generated);
        ScenarioExpect.Contains("public bool TryGet(string key, out global::TestNamespace.Glyph value)", generated);
        ScenarioExpect.Contains("private void Trim()", generated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsMissingFactory")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKFLY002");
    }

    [Scenario("ReportsInvalidLruConfiguration")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKFLY006");
    }

    [Scenario("ReportsNonPartialAndNonStaticFactory")]
    [Fact]
    public void ReportsNonPartialAndNonStaticFactory()
    {
        const string source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string))]
            public readonly record struct NonPartialGlyph(char Value)
            {
                [FlyweightFactory]
                private static NonPartialGlyph Create(string key) => new(key[0]);
            }

            [Flyweight(typeof(string))]
            public readonly partial record struct NonStaticFactoryGlyph(char Value)
            {
                [FlyweightFactory]
                private NonStaticFactoryGlyph Create(string key) => new(key[0]);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsNonPartialAndNonStaticFactory));
        var gen = new FlyweightGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        ScenarioExpect.Contains(diags, d => d.Id == "PKFLY001");
        ScenarioExpect.Contains(diags, d => d.Id == "PKFLY004");
    }
}
