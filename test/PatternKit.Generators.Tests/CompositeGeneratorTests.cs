using PatternKit.Generators.Composite;

namespace PatternKit.Generators.Tests;

public class CompositeGeneratorTests
{
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

        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        Assert.Contains(generated, s => s.HintName == "ICategory.Composite.g.cs");
        Assert.Contains(generated, s => s.HintName == "ICategory.Composite.Traversal.g.cs");

        var sourceText = generated.First(s => s.HintName == "ICategory.Composite.g.cs").SourceText.ToString();
        Assert.Contains("public abstract partial class CategoryComponentBase : global::TestNamespace.ICategory", sourceText);
        Assert.Contains("public override void Add(global::TestNamespace.ICategory child)", sourceText);
        Assert.Contains("throw new global::System.NotSupportedException()", sourceText);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

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
        Assert.Contains(diags, d => d.Id == "PKCMP001");
    }

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
        Assert.Contains(diags, d => d.Id == "PKCMP002");
    }
}
