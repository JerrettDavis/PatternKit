using PatternKit.Generators.Composite;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class CompositeGeneratorTests
{
    [Scenario("GeneratesCompositeBasesAndTraversalHelpers")]
    [Fact]
    public void GeneratesCompositeBasesAndTraversalHelpers()
    {
        const string source = """
            using PatternKit.Generators.Composite;

            namespace TestNamespace;

            [CompositeComponent(GenerateTraversalHelpers = true)]
            public partial interface ICategory
            {
                string Name { get; }
            }

            public sealed class CategoryLeaf : CategoryComponentBase
            {
                public CategoryLeaf(string name) => Name = name;
                public override string Name { get; }
            }

            public sealed class CategoryNode : CategoryCompositeBase
            {
                public CategoryNode(string name) => Name = name;
                public override string Name { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesCompositeBasesAndTraversalHelpers));
        var gen = new CompositeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        ScenarioExpect.Contains(generated, s => s.HintName == "ICategory.Composite.g.cs");
        ScenarioExpect.Contains(generated, s => s.HintName == "ICategory.Composite.Traversal.g.cs");

        var sourceText = generated.First(s => s.HintName == "ICategory.Composite.g.cs").SourceText.ToString();
        ScenarioExpect.Contains("public abstract partial class CategoryComponentBase : global::TestNamespace.ICategory", sourceText);
        ScenarioExpect.Contains("public override void Add(global::TestNamespace.ICategory child)", sourceText);
        ScenarioExpect.Contains("throw new global::System.NotSupportedException()", sourceText);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDiagnosticWhenComponentIsNotPartial")]
    [Fact]
    public void ReportsDiagnosticWhenComponentIsNotPartial()
    {
        const string source = """
            using PatternKit.Generators.Composite;

            namespace TestNamespace;

            [CompositeComponent]
            public interface ICategory
            {
                string Name { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsDiagnosticWhenComponentIsNotPartial));
        var gen = new CompositeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKCMP001");
    }

    [Scenario("ReportsDiagnosticWhenComponentIsConcrete")]
    [Fact]
    public void ReportsDiagnosticWhenComponentIsConcrete()
    {
        const string source = """
            using PatternKit.Generators.Composite;

            namespace TestNamespace;

            [CompositeComponent]
            public partial class Category
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsDiagnosticWhenComponentIsConcrete));
        var gen = new CompositeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKCMP002");
    }
}
