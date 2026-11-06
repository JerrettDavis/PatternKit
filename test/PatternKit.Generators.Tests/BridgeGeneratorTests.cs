using PatternKit.Generators.Bridge;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public class BridgeGeneratorTests
{
    [Scenario("GeneratesBridgeForwardingAndDefaultType")]
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

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        ScenarioExpect.Contains(generated, s => s.HintName == "Shape.Bridge.g.cs");
        ScenarioExpect.Contains(generated, s => s.HintName == "DefaultShape.Bridge.Default.g.cs");

        var bridgeSource = generated.First(s => s.HintName == "Shape.Bridge.g.cs").SourceText.ToString();
        ScenarioExpect.Contains("protected global::TestNamespace.IRenderer Implementor { get; }", bridgeSource);
        ScenarioExpect.Contains("protected void DrawLine(int x1, int y1, int x2, int y2)", bridgeSource);
        ScenarioExpect.Contains("protected global::System.Threading.Tasks.ValueTask FlushAsync", bridgeSource);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("ReportsDiagnosticWhenAbstractionIsNotPartial")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKBRG001");
    }

    [Scenario("ReportsDiagnosticWhenImplementorIsConcrete")]
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
        ScenarioExpect.Contains(diags, d => d.Id == "PKBRG002");
    }

    [Scenario("GeneratesGenericBridgeWithForwardedPropertiesRefParametersAndDefaults")]
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

        ScenarioExpect.All(result.Results, r => ScenarioExpect.Empty(r.Diagnostics));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "Store.Bridge.g.cs").SourceText.ToString();
        ScenarioExpect.Contains("public abstract partial record class Store<T>", generated);
        ScenarioExpect.Contains("protected string Name => Backend.Name;", generated);
        ScenarioExpect.Contains("ref int source, out int destination, in bool enabled", generated);
        ScenarioExpect.Contains("Backend.Copy(ref source, out destination, in enabled)", generated);
        ScenarioExpect.Contains(@"string value = @""quoted""", generated);
        ScenarioExpect.Contains("bool enabled = true", generated);
        ScenarioExpect.Contains("int count = 3", generated);
        ScenarioExpect.Contains("object? state = default", generated);
        ScenarioExpect.DoesNotContain("Ignored", generated);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success, string.Join("\n", updated.GetDiagnostics()));
    }

    [Scenario("ReportsDiagnosticForEventMembersAndDefaultNameConflicts")]
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

        ScenarioExpect.Contains(eventResult.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKBRG003");
        ScenarioExpect.Contains(conflictResult.Results.SelectMany(r => r.Diagnostics), d => d.Id == "PKBRG004");
    }
}
