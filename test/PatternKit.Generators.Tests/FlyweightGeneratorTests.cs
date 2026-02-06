using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class FlyweightGeneratorTests
{
    [Fact]
    public void Generates_Flyweight_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string))]
            public partial class Glyph
            {
                public char Character { get; }
                public int FontSize { get; }

                private Glyph(char character, int fontSize)
                {
                    Character = character;
                    FontSize = fontSize;
                }

                [FlyweightFactory]
                public static Glyph Create(string key)
                {
                    return new Glyph(key[0], 12);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Flyweight_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Glyph.Flyweight.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generated_Cache_Has_Get_TryGet_Clear()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(int))]
            public partial class ColorValue
            {
                public int R { get; }
                public int G { get; }
                public int B { get; }

                private ColorValue(int r, int g, int b) { R = r; G = g; B = b; }

                [FlyweightFactory]
                public static ColorValue Create(int key)
                {
                    return new ColorValue(key & 0xFF, (key >> 8) & 0xFF, (key >> 16) & 0xFF);
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generated_Cache_Has_Get_TryGet_Clear));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Flyweight"))
            .SourceText.ToString();

        Assert.Contains("public static class ColorValueCache", generatedSource);
        Assert.Contains("public static", generatedSource);
        Assert.Contains("Get(", generatedSource);
        Assert.Contains("TryGet(", generatedSource);
        Assert.Contains("Clear()", generatedSource);
        Assert.Contains("Count", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Custom_Cache_Name()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string), CacheTypeName = "GlyphPool")]
            public partial class Glyph
            {
                [FlyweightFactory]
                public static Glyph Create(string key) => new Glyph();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Custom_Cache_Name));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Flyweight"))
            .SourceText.ToString();

        Assert.Contains("public static class GlyphPool", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Without_TryGet_When_Disabled()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string), GenerateTryGet = false)]
            public partial class Glyph
            {
                [FlyweightFactory]
                public static Glyph Create(string key) => new Glyph();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Without_TryGet_When_Disabled));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Flyweight"))
            .SourceText.ToString();

        Assert.Contains("Get(", generatedSource);
        Assert.DoesNotContain("TryGet(", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Concurrent_Threading()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string), Threading = FlyweightThreadingPolicy.Concurrent)]
            public partial class Glyph
            {
                [FlyweightFactory]
                public static Glyph Create(string key) => new Glyph();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Concurrent_Threading));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generatedSource = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Flyweight"))
            .SourceText.ToString();

        Assert.Contains("ConcurrentDictionary", generatedSource);
        Assert.Contains("GetOrAdd", generatedSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string))]
            public class Glyph
            {
                [FlyweightFactory]
                public static Glyph Create(string key) => new Glyph();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKFLY001");
    }

    [Fact]
    public void Reports_Error_When_No_Factory()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string))]
            public partial class Glyph
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_No_Factory));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKFLY002");
    }

    [Fact]
    public void Reports_Error_When_Multiple_Factories()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string))]
            public partial class Glyph
            {
                [FlyweightFactory]
                public static Glyph Create(string key) => new Glyph();

                [FlyweightFactory]
                public static Glyph CreateAlt(string key) => new Glyph();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Multiple_Factories));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKFLY003");
    }

    [Fact]
    public void Reports_Error_When_Factory_Not_Static()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string))]
            public partial class Glyph
            {
                [FlyweightFactory]
                public Glyph Create(string key) => new Glyph();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Factory_Not_Static));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKFLY004");
    }

    [Fact]
    public void Reports_Error_When_Lru_Without_Capacity()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(string), Eviction = FlyweightEviction.Lru)]
            public partial class Glyph
            {
                [FlyweightFactory]
                public static Glyph Create(string key) => new Glyph();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Lru_Without_Capacity));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKFLY006");
    }

    [Fact]
    public void Generates_Flyweight_For_Struct()
    {
        var source = """
            using PatternKit.Generators.Flyweight;

            namespace TestNamespace;

            [Flyweight(typeof(int))]
            public partial struct Color
            {
                public byte R;
                public byte G;
                public byte B;

                [FlyweightFactory]
                public static Color Create(int rgb)
                {
                    return new Color { R = (byte)(rgb >> 16), G = (byte)(rgb >> 8), B = (byte)rgb };
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Flyweight_For_Struct));
        _ = RoslynTestHelpers.Run(comp, new FlyweightGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
