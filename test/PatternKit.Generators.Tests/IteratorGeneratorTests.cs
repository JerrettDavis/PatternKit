using PatternKit.Generators.Iterator;

namespace PatternKit.Generators.Tests;

public class IteratorGeneratorTests
{
    [Fact]
    public void GeneratesIteratorMembers()
    {
        const string source = """
            using PatternKit.Generators.Iterator;

            namespace TestNamespace;

            [Iterator]
            public partial struct Counter
            {
                private int _current;

                [IteratorStep]
                private bool Step(out int item)
                {
                    item = ++_current;
                    return item <= 3;
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesIteratorMembers));
        var gen = new IteratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "Counter.Iterator.g.cs").SourceText.ToString();
        Assert.Contains("public bool TryMoveNext(out int item)", generated);
        Assert.Contains("public struct Enumerator", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ReportsMissingStep()
    {
        const string source = """
            using PatternKit.Generators.Iterator;

            namespace TestNamespace;

            [Iterator]
            public partial struct Counter
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsMissingStep));
        var gen = new IteratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);
        Assert.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKIT002");
    }

    [Fact]
    public void ReportsNonPartialAndBadStepSignature()
    {
        const string source = """
            using PatternKit.Generators.Iterator;

            namespace TestNamespace;

            [Iterator]
            public struct NonPartialCounter
            {
                [IteratorStep]
                private bool Step(out int item)
                {
                    item = 1;
                    return true;
                }
            }

            [Iterator]
            public partial struct BadStepCounter
            {
                [IteratorStep]
                private int Step() => 1;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsNonPartialAndBadStepSignature));
        var gen = new IteratorGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKIT001");
        Assert.Contains(diagnostics, d => d.Id == "PKIT004");
    }
}
