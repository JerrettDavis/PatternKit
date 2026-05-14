using PatternKit.Generators.Bridge;

namespace PatternKit.Generators.Tests;

public class BridgeGeneratorTests
{
    [Fact]
    public void GeneratesBridgeForwardingAndDefaultType()
    {
        const string source = """
            using PatternKit.Generators.Bridge;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [BridgeImplementor]
            public partial interface IRenderer
            {
                void DrawLine(int x1, int y1, int x2, int y2);
                ValueTask FlushAsync(CancellationToken ct = default);
            }

            [BridgeAbstraction(typeof(IRenderer), GenerateDefault = true, DefaultTypeName = "DefaultShape")]
            public partial class Shape
            {
                public void Draw() => DrawLine(0, 0, 1, 1);
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesBridgeForwardingAndDefaultType));
        var gen = new BridgeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        Assert.Contains(generated, s => s.HintName == "Shape.Bridge.g.cs");
        Assert.Contains(generated, s => s.HintName == "DefaultShape.Bridge.Default.g.cs");

        var bridgeSource = generated.First(s => s.HintName == "Shape.Bridge.g.cs").SourceText.ToString();
        Assert.Contains("protected global::TestNamespace.IRenderer Implementor { get; }", bridgeSource);
        Assert.Contains("protected void DrawLine(int x1, int y1, int x2, int y2)", bridgeSource);
        Assert.Contains("protected global::System.Threading.Tasks.ValueTask FlushAsync", bridgeSource);

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Fact]
    public void ReportsDiagnosticWhenAbstractionIsNotPartial()
    {
        const string source = """
            using PatternKit.Generators.Bridge;

            namespace TestNamespace;

            public interface IRenderer
            {
                void Draw();
            }

            [BridgeAbstraction(typeof(IRenderer))]
            public class Shape
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsDiagnosticWhenAbstractionIsNotPartial));
        var gen = new BridgeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKBRG001");
    }

    [Fact]
    public void ReportsDiagnosticWhenImplementorIsConcrete()
    {
        const string source = """
            using PatternKit.Generators.Bridge;

            namespace TestNamespace;

            public sealed class Renderer
            {
                public void Draw() { }
            }

            [BridgeAbstraction(typeof(Renderer))]
            public partial class Shape
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(ReportsDiagnosticWhenImplementorIsConcrete));
        var gen = new BridgeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out _);

        var diags = result.Results.SelectMany(r => r.Diagnostics);
        Assert.Contains(diags, d => d.Id == "PKBRG002");
    }

    [Fact]
    public void GeneratesGenericBridgeWithForwardedPropertiesRefParametersAndDefaults()
    {
        const string source = """
            using PatternKit.Generators.Bridge;

            [BridgeImplementor]
            public abstract class Repository<T>
            {
                public abstract string Name { get; }
                public abstract void Copy(ref int source, out int destination, in bool enabled);
                public abstract string Format(string value = "quoted", bool enabled = true, int count = 3, object? state = null);
                [BridgeIgnore] public abstract void Ignored();
                public static void StaticUtility() { }
            }

            [BridgeAbstraction(typeof(Repository<string>), ImplementorPropertyName = "Backend")]
            public partial record Store<T>
            {
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(GeneratesGenericBridgeWithForwardedPropertiesRefParametersAndDefaults));
        var gen = new BridgeGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var result, out var updated);

        Assert.All(result.Results, r => Assert.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "Store.Bridge.g.cs").SourceText.ToString();
        Assert.Contains("public abstract partial record class Store<T>", generated);
        Assert.Contains("protected string Name => Backend.Name;", generated);
        Assert.Contains("ref int source, out int destination, in bool enabled", generated);
        Assert.Contains("Backend.Copy(ref source, out destination, in enabled)", generated);
        Assert.Contains(@"string value = @""quoted""", generated);
        Assert.Contains("bool enabled = true", generated);
        Assert.Contains("int count = 3", generated);
        Assert.Contains("object? state = default", generated);
        Assert.DoesNotContain("Ignored", generated);
        Assert.True(updated.Emit(Stream.Null).Success, string.Join("\n", updated.GetDiagnostics()));
    }

    [Fact]
    public void ReportsDiagnosticForEventMembersAndDefaultNameConflicts()
    {
        const string unsupportedEvent = """
            using PatternKit.Generators.Bridge;

            namespace TestNamespace;

            public interface IRenderer
            {
                event System.Action? Rendered;
            }

            [BridgeAbstraction(typeof(IRenderer))]
            public partial class Shape
            {
            }
            """;
        const string conflictingDefault = """
            using PatternKit.Generators.Bridge;

            namespace TestNamespace;

            public interface IRenderer
            {
                void Draw();
            }

            public sealed class ExistingShape
            {
            }

            [BridgeAbstraction(typeof(IRenderer), GenerateDefault = true, DefaultTypeName = "ExistingShape")]
            public partial class Shape
            {
            }
            """;

        var eventComp = RoslynTestHelpers.CreateCompilation(unsupportedEvent, nameof(ReportsDiagnosticForEventMembersAndDefaultNameConflicts) + "Event");
        var conflictComp = RoslynTestHelpers.CreateCompilation(conflictingDefault, nameof(ReportsDiagnosticForEventMembersAndDefaultNameConflicts) + "Conflict");
        var gen = new BridgeGenerator();

        _ = RoslynTestHelpers.Run(eventComp, gen, out var eventResult, out _);
        _ = RoslynTestHelpers.Run(conflictComp, gen, out var conflictResult, out _);

        Assert.Contains(eventResult.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKBRG003");
        Assert.Contains(conflictResult.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKBRG004");
    }
}
