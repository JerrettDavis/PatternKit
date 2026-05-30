using Microsoft.CodeAnalysis;
using PatternKit.Generators.Timeouts;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Timeout Manager generator")]
public sealed partial class TimeoutManagerGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates timeout manager factory")]
    [Fact]
    public Task Generates_Timeout_Manager_Factory()
        => Given("a timeout manager declaration", () => Compile("""
            using PatternKit.Generators.Timeouts;
            namespace Demo;
            [GenerateTimeoutManager(typeof(string), FactoryMethodName = "Build", ManagerName = "reservation-timeouts")]
            public static partial class ReservationTimeouts;
            """))
        .Then("the generated source creates the configured manager", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class ReservationTimeouts", source);
            ScenarioExpect.Contains("TimeoutManager<string> Build()", source);
            ScenarioExpect.Contains("TimeoutManager<string>.Create(\"reservation-timeouts\").Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid timeout manager declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Timeout_Manager_Declarations()
        => Given("invalid timeout manager declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.Timeouts;
                [GenerateTimeoutManager(typeof(string))]
                public static class ReservationTimeouts;
                """),
            Compile("""
                using PatternKit.Generators.Timeouts;
                [GenerateTimeoutManager(typeof(string), FactoryMethodName = "")]
                public static partial class ReservationTimeouts;
                """),
            Compile("""
                using PatternKit.Generators.Timeouts;
                [GenerateTimeoutManager(typeof(string), ManagerName = "   ")]
                public static partial class ReservationTimeouts;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKTM001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKTM002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKTM002");
        })
        .AssertPassed();

    [Scenario("Generates timeout manager defaults and nested host wrappers")]
    [Fact]
    public Task Generates_Timeout_Manager_Defaults_And_Nested_Host_Wrappers()
        => Given("nested timeout manager declarations", () => Compile("""
            using PatternKit.Generators.Timeouts;
            namespace Demo;
            public static partial class FulfillmentModule
            {
                internal abstract partial class Timeouts
                {
                    [GenerateTimeoutManager(typeof(System.Guid), ManagerName = "order\\\"timeouts")]
                    private sealed partial class ReservationTimeouts;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class FulfillmentModule", source);
            ScenarioExpect.Contains("internal abstract partial class Timeouts", source);
            ScenarioExpect.Contains("private sealed partial class ReservationTimeouts", source);
            ScenarioExpect.Contains("TimeoutManager<global::System.Guid>.Create(\"order\\\\\\\"timeouts\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates timeout manager factory for global namespace struct host")]
    [Fact]
    public Task Generates_Timeout_Manager_Factory_For_Global_Namespace_Struct_Host()
        => Given("a struct timeout manager host without a namespace", () => Compile("""
            using PatternKit.Generators.Timeouts;
            [GenerateTimeoutManager(typeof(int))]
            internal partial struct ReservationTimeouts;
            """))
        .Then("the generated source preserves the struct host shape", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("internal partial struct ReservationTimeouts", source);
            ScenarioExpect.Contains("TimeoutManager<int> Create()", source);
            ScenarioExpect.Contains("TimeoutManager<int>.Create(\"timeout-manager\").Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips timeout manager generation for malformed key type")]
    [Fact]
    public Task Skips_Timeout_Manager_Generation_For_Malformed_Key_Type()
        => Given("a timeout manager declaration with an unresolved key type", () => Compile("""
            using PatternKit.Generators.Timeouts;
            [GenerateTimeoutManager(typeof(MissingKey))]
            public static partial class MissingTimeouts;
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
            "TimeoutManagerGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Application.Timeouts.TimeoutManager<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new TimeoutManagerGenerator(), out var run, out var updated);
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
