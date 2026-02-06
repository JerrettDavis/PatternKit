using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class CompositeGeneratorTests
{
    [Fact]
    public void Generates_Composite_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public partial interface IGraphic
            {
                void Draw();
                void Move(int x, int y);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Composite_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("IGraphic.Composite.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_ComponentBase_And_CompositeBase()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public partial interface IGraphic
            {
                void Draw();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_ComponentBase_And_CompositeBase));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("IGraphic.Composite"))
            .SourceText.ToString();

        Assert.Contains("class GraphicBase", generated);
        Assert.Contains("class GraphicComposite", generated);
        Assert.Contains("void Draw()", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Add_And_Remove_Methods()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public partial interface IGraphic
            {
                void Draw();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Add_And_Remove_Methods));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("IGraphic.Composite"))
            .SourceText.ToString();

        Assert.Contains("public void Add(", generated);
        Assert.Contains("public void Remove(", generated);
        Assert.Contains("Children", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Custom_Names()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent(ComponentBaseName = "Leaf", CompositeBaseName = "Group", ChildrenPropertyName = "Items")]
            public partial interface IGraphic
            {
                void Draw();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Custom_Names));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("IGraphic.Composite"))
            .SourceText.ToString();

        Assert.Contains("class Leaf", generated);
        Assert.Contains("class Group", generated);
        Assert.Contains("Items", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Traversal_Helpers()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent(GenerateTraversalHelpers = true)]
            public partial interface IGraphic
            {
                void Draw();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Traversal_Helpers));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("IGraphic.Composite.g.cs", names);
        Assert.Contains("IGraphic.Composite.Traversal.g.cs", names);

        var traversal = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Traversal"))
            .SourceText.ToString();

        Assert.Contains("DepthFirst()", traversal);
        Assert.Contains("BreadthFirst()", traversal);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Ignores_Members_With_CompositeIgnore()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public partial interface IGraphic
            {
                void Draw();
                [CompositeIgnore]
                void Debug();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Ignores_Members_With_CompositeIgnore));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("IGraphic.Composite"))
            .SourceText.ToString();

        Assert.Contains("Draw()", generated);
        // Ignored members are declared as abstract (not delegated to children)
        Assert.Contains("abstract", generated);
        Assert.DoesNotContain("Delegates Debug", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Forwards_Properties_In_Composite()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public partial interface IGraphic
            {
                string Name { get; set; }
                void Draw();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Forwards_Properties_In_Composite));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("IGraphic.Composite"))
            .SourceText.ToString();

        // ComponentBase has default property
        Assert.Contains("string Name { get; set; }", generated);
        // CompositeBase delegates
        Assert.Contains("Draw()", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Handles_Methods_With_Return_Values()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public partial interface IGraphic
            {
                int GetArea();
                void Draw();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Handles_Methods_With_Return_Values));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("IGraphic.Composite"))
            .SourceText.ToString();

        Assert.Contains("int GetArea()", generated);
        // Composite returns last child's result
        Assert.Contains("__result", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public interface IGraphic
            {
                void Draw();
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCPS001");
    }

    [Fact]
    public void Reports_Error_When_Not_Interface_Or_Abstract()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public partial class ConcreteGraphic
            {
                public void Draw() { }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Not_Interface_Or_Abstract));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKCPS002");
    }

    [Fact]
    public void Works_With_Abstract_Class_Component()
    {
        var source = """
            using PatternKit.Generators.Composite;

            namespace TestNs;

            [CompositeComponent]
            public abstract partial class GraphicComponent
            {
                public abstract void Draw();
                public abstract string Name { get; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Works_With_Abstract_Class_Component));
        _ = RoslynTestHelpers.Run(comp, new CompositeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
