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

    [Scenario("GeneratesCompositeBasesForAbstractClassWithCustomNames")]
    [Fact]
    public void GeneratesCompositeBasesForAbstractClassWithCustomNames()
    {
        const string source = """
            using PatternKit.Generators.Composite;

            namespace TestNamespace;

            [CompositeComponent(
                ComponentBaseName = "MenuItemBase",
                CompositeBaseName = "MenuGroupBase",
                ChildrenPropertyName = "Items")]
            public abstract partial class MenuItem
            {
                public abstract string Name { get; }

                [CompositeIgnore]
                public abstract int SortOrder { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesCompositeBasesForAbstractClassWithCustomNames));
        var gen = new CompositeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));

        var generated = ScenarioExpect.Single(result.Results.SelectMany(r => r.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("MenuItem.Composite.g.cs", generated.HintName);
        ScenarioExpect.Contains("public abstract partial class MenuItemBase : global::TestNamespace.MenuItem", text);
        ScenarioExpect.Contains("public virtual global::System.Collections.Generic.IReadOnlyList<global::TestNamespace.MenuItem> Items", text);
        ScenarioExpect.Contains("public abstract override string Name { get; }", text);
        ScenarioExpect.DoesNotContain("SortOrder", text);
        ScenarioExpect.Contains("public abstract partial class MenuGroupBase : MenuItemBase", text);

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

    [Scenario("ReportsDiagnosticWhenCompositeContractHasEvent")]
    [Fact]
    public void ReportsDiagnosticWhenCompositeContractHasEvent()
    {
        const string source = """
            using System;
            using PatternKit.Generators.Composite;

            namespace TestNamespace;

            [CompositeComponent]
            public partial interface ICategory
            {
                event EventHandler? Changed;
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsDiagnosticWhenCompositeContractHasEvent));
        var gen = new CompositeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKCMP004");
    }

    [Scenario("ReportsDiagnosticWhenCompositeGeneratedNameConflicts")]
    [Fact]
    public void ReportsDiagnosticWhenCompositeGeneratedNameConflicts()
    {
        const string source = """
            using PatternKit.Generators.Composite;

            namespace TestNamespace;

            public abstract class CategoryComponentBase;

            [CompositeComponent]
            public partial interface ICategory
            {
                string Name { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsDiagnosticWhenCompositeGeneratedNameConflicts));
        var gen = new CompositeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics);
        ScenarioExpect.Contains(diags, d => d.Id == "PKCMP003");
    }
}
