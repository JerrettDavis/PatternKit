using Microsoft.CodeAnalysis;
using PatternKit.Generators.ManualTaskGates;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Manual Task Gate generator")]
public sealed partial class ManualTaskGateGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates manual task gate factory")]
    [Fact]
    public Task Generates_Manual_Task_Gate_Factory()
        => Given("a manual task gate declaration", () => Compile("""
            using PatternKit.Generators.ManualTaskGates;
            namespace Demo;
            [GenerateManualTaskGate(typeof(string), FactoryMethodName = "Build", GateName = "approval-gate")]
            public static partial class ApprovalGates;
            """))
        .Then("the generated source creates the configured gate", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class ApprovalGates", source);
            ScenarioExpect.Contains("ManualTaskGate<string> Build()", source);
            ScenarioExpect.Contains("ManualTaskGate<string>.Create(\"approval-gate\").Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid manual task gate declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Manual_Task_Gate_Declarations()
        => Given("invalid manual task gate declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.ManualTaskGates;
                [GenerateManualTaskGate(typeof(string))]
                public static class ApprovalGates;
                """),
            Compile("""
                using PatternKit.Generators.ManualTaskGates;
                [GenerateManualTaskGate(typeof(string), FactoryMethodName = "")]
                public static partial class ApprovalGates;
                """),
            Compile("""
                using PatternKit.Generators.ManualTaskGates;
                [GenerateManualTaskGate(typeof(string), GateName = "   ")]
                public static partial class ApprovalGates;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKMTG001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKMTG002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKMTG002");
        })
        .AssertPassed();

    [Scenario("Generates manual task gate defaults and nested host wrappers")]
    [Fact]
    public Task Generates_Manual_Task_Gate_Defaults_And_Nested_Host_Wrappers()
        => Given("nested manual task gate declarations", () => Compile("""
            using PatternKit.Generators.ManualTaskGates;
            namespace Demo;
            public static partial class FulfillmentModule
            {
                internal abstract partial class Gates
                {
                    [GenerateManualTaskGate(typeof(System.Guid), GateName = "approval\\\"gate")]
                    private sealed partial class ManualApprovals;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class FulfillmentModule", source);
            ScenarioExpect.Contains("internal abstract partial class Gates", source);
            ScenarioExpect.Contains("private sealed partial class ManualApprovals", source);
            ScenarioExpect.Contains("ManualTaskGate<global::System.Guid>.Create(\"approval\\\\\\\"gate\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates manual task gate factory for global namespace struct host")]
    [Fact]
    public Task Generates_Manual_Task_Gate_Factory_For_Global_Namespace_Struct_Host()
        => Given("a struct manual task gate host without a namespace", () => Compile("""
            using PatternKit.Generators.ManualTaskGates;
            [GenerateManualTaskGate(typeof(int))]
            internal partial struct ApprovalGates;
            """))
        .Then("the generated source preserves the struct host shape", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("internal partial struct ApprovalGates", source);
            ScenarioExpect.Contains("ManualTaskGate<int> Create()", source);
            ScenarioExpect.Contains("ManualTaskGate<int>.Create(\"manual-task-gate\").Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips manual task gate generation for malformed key type")]
    [Fact]
    public Task Skips_Manual_Task_Gate_Generation_For_Malformed_Key_Type()
        => Given("a manual task gate declaration with an unresolved key type", () => Compile("""
            using PatternKit.Generators.ManualTaskGates;
            [GenerateManualTaskGate(typeof(MissingKey))]
            public static partial class MissingGates;
            """))
        .Then("no generated source is produced by the generator", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Empty(result.GeneratedSources);
            ScenarioExpect.False(result.EmitSuccess);
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "ManualTaskGateGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Application.ManualTaskGates.ManualTaskGate<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new ManualTaskGateGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
