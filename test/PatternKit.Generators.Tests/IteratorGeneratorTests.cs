using PatternKit.Generators.Iterator;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class IteratorGeneratorTests
{
    [Scenario("GeneratesIteratorMembers")]
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

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "Counter.Iterator.g.cs").SourceText.ToString();
        ScenarioExpect.Contains("public bool TryMoveNext(out int item)", generated);
        ScenarioExpect.Contains("public struct Enumerator", generated);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsMissingStep")]
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
        ScenarioExpect.Contains(result.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKIT002");
    }

    [Scenario("ReportsNonPartialAndBadStepSignature")]
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
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKIT001");
        ScenarioExpect.Contains(diagnostics, d => d.Id == "PKIT004");
    }
}
