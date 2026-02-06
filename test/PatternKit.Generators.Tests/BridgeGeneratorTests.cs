using Microsoft.CodeAnalysis;

namespace PatternKit.Generators.Tests;

public class BridgeGeneratorTests
{
    [Fact]
    public void Generates_Bridge_Without_Diagnostics()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public interface IRenderer
            {
                void DrawLine(int x1, int y1, int x2, int y2);
                void DrawCircle(int cx, int cy, int radius);
            }

            [BridgeAbstraction(typeof(IRenderer))]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Bridge_Without_Diagnostics));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Shape.Bridge.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Constructor_And_Property()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public interface IRenderer
            {
                void DrawLine(int x1, int y1, int x2, int y2);
            }

            [BridgeAbstraction(typeof(IRenderer))]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Constructor_And_Property));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Shape.Bridge"))
            .SourceText.ToString();

        Assert.Contains("protected Shape(", generated);
        Assert.Contains("protected global::TestNs.IRenderer Implementor { get; }", generated);
        Assert.Contains("protected void DrawLine(", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Custom_Property_Name()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public interface IRenderer
            {
                void Draw();
            }

            [BridgeAbstraction(typeof(IRenderer), ImplementorPropertyName = "Renderer")]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Custom_Property_Name));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Shape.Bridge"))
            .SourceText.ToString();

        Assert.Contains("protected global::TestNs.IRenderer Renderer { get; }", generated);
        Assert.Contains("Renderer.Draw()", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Forwards_Properties()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public interface IRenderer
            {
                string Name { get; }
                int Width { get; set; }
            }

            [BridgeAbstraction(typeof(IRenderer))]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Forwards_Properties));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Shape.Bridge"))
            .SourceText.ToString();

        Assert.Contains("Implementor.Name", generated);
        Assert.Contains("Implementor.Width", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Ignores_Members_With_BridgeIgnore()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public interface IRenderer
            {
                void Draw();
                [BridgeIgnore]
                void Debug();
            }

            [BridgeAbstraction(typeof(IRenderer))]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Ignores_Members_With_BridgeIgnore));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Shape.Bridge"))
            .SourceText.ToString();

        Assert.Contains("Draw()", generated);
        Assert.DoesNotContain("Debug()", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Generates_Default_Implementor()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public interface IRenderer
            {
                void Draw();
                string Name { get; }
            }

            [BridgeAbstraction(typeof(IRenderer), GenerateDefault = true)]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generates_Default_Implementor));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var names = run.Results.SelectMany(r => r.GeneratedSources).Select(gs => gs.HintName).ToArray();
        Assert.Contains("Shape.Bridge.g.cs", names);
        Assert.Contains("Shape.Bridge.Default.g.cs", names);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Reports_Error_When_Type_Not_Partial()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public interface IRenderer
            {
                void Draw();
            }

            [BridgeAbstraction(typeof(IRenderer))]
            public class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Type_Not_Partial));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKBRG001");
    }

    [Fact]
    public void Reports_Error_When_Implementor_Not_Abstract()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public class ConcreteRenderer
            {
                public void Draw() { }
            }

            [BridgeAbstraction(typeof(ConcreteRenderer))]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Reports_Error_When_Implementor_Not_Abstract));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out _);

        var diagnostics = run.Results.SelectMany(r => r.Diagnostics).ToArray();
        Assert.Contains(diagnostics, d => d.Id == "PKBRG002");
    }

    [Fact]
    public void Works_With_Abstract_Class_Implementor()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public abstract class RendererBase
            {
                public abstract void Draw();
                public abstract string Name { get; }
            }

            [BridgeAbstraction(typeof(RendererBase))]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Works_With_Abstract_Class_Implementor));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void Handles_Method_Return_Values()
    {
        var source = """
            using PatternKit.Generators.Bridge;

            namespace TestNs;

            [BridgeImplementor]
            public interface IRenderer
            {
                int GetWidth();
                string Render(string input);
            }

            [BridgeAbstraction(typeof(IRenderer))]
            public partial class Shape { }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Handles_Method_Return_Values));
        _ = RoslynTestHelpers.Run(comp, new BridgeGenerator(), out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .First(gs => gs.HintName.Contains("Shape.Bridge"))
            .SourceText.ToString();

        Assert.Contains("protected int GetWidth()", generated);
        Assert.Contains("=> Implementor.GetWidth()", generated);
        Assert.Contains("protected string Render(string input)", generated);
        Assert.Contains("=> Implementor.Render(input)", generated);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }
}
