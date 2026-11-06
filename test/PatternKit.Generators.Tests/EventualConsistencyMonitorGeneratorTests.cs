using Microsoft.CodeAnalysis;
using PatternKit.Generators.EventualConsistency;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Eventual Consistency Monitor generator")]
public sealed partial class EventualConsistencyMonitorGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates eventual consistency monitor factory")]
    [Fact]
    public Task Generates_Eventual_Consistency_Monitor_Factory()
        => Given("an eventual consistency monitor declaration", () => Compile("""
            using PatternKit.Generators.EventualConsistency;
            namespace Demo;
            [GenerateEventualConsistencyMonitor(typeof(string), FactoryMethodName = "Build", MonitorName = "order-consistency", MaxAllowedLag = 2)]
            public static partial class OrderConsistency;
            """))
        .Then("the generated source creates the configured monitor", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class OrderConsistency", source);
            ScenarioExpect.Contains("EventualConsistencyMonitor<string> Build()", source);
            ScenarioExpect.Contains("EventualConsistencyMonitor<string>.Create(\"order-consistency\").WithMaxAllowedLag(2).Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid eventual consistency monitor declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Eventual_Consistency_Monitor_Declarations()
        => Given("invalid eventual consistency monitor declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.EventualConsistency;
                [GenerateEventualConsistencyMonitor(typeof(string))]
                public static class OrderConsistency;
                """),
            Compile("""
                using PatternKit.Generators.EventualConsistency;
                [GenerateEventualConsistencyMonitor(typeof(string), FactoryMethodName = "")]
                public static partial class OrderConsistency;
                """),
            Compile("""
                using PatternKit.Generators.EventualConsistency;
                [GenerateEventualConsistencyMonitor(typeof(string), MonitorName = "   ")]
                public static partial class OrderConsistency;
                """),
            Compile("""
                using PatternKit.Generators.EventualConsistency;
                [GenerateEventualConsistencyMonitor(typeof(string), MaxAllowedLag = -1)]
                public static partial class OrderConsistency;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKECM001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKECM002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKECM002");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKECM002");
        })
        .AssertPassed();

    [Scenario("Generates eventual consistency monitor defaults and nested host wrappers")]
    [Fact]
    public Task Generates_Eventual_Consistency_Monitor_Defaults_And_Nested_Host_Wrappers()
        => Given("nested eventual consistency monitor declarations", () => Compile("""
            using PatternKit.Generators.EventualConsistency;
            namespace Demo;
            public static partial class FulfillmentModule
            {
                internal abstract partial class Consistency
                {
                    [GenerateEventualConsistencyMonitor(typeof(System.Guid), MonitorName = "order\\\"consistency")]
                    private sealed partial class OrderConsistency;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class FulfillmentModule", source);
            ScenarioExpect.Contains("internal abstract partial class Consistency", source);
            ScenarioExpect.Contains("private sealed partial class OrderConsistency", source);
            ScenarioExpect.Contains("EventualConsistencyMonitor<global::System.Guid>.Create(\"order\\\\\\\"consistency\").WithMaxAllowedLag(0).Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates eventual consistency monitor factory for global namespace struct host")]
    [Fact]
    public Task Generates_Eventual_Consistency_Monitor_Factory_For_Global_Namespace_Struct_Host()
        => Given("a struct eventual consistency monitor host without a namespace", () => Compile("""
            using PatternKit.Generators.EventualConsistency;
            [GenerateEventualConsistencyMonitor(typeof(int))]
            internal partial struct OrderConsistency;
            """))
        .Then("the generated source preserves the struct host shape", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("internal partial struct OrderConsistency", source);
            ScenarioExpect.Contains("EventualConsistencyMonitor<int> Create()", source);
            ScenarioExpect.Contains("EventualConsistencyMonitor<int>.Create(\"eventual-consistency-monitor\").WithMaxAllowedLag(0).Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips eventual consistency monitor generation for malformed key type")]
    [Fact]
    public Task Skips_Eventual_Consistency_Monitor_Generation_For_Malformed_Key_Type()
        => Given("an eventual consistency monitor declaration with an unresolved key type", () => Compile("""
            using PatternKit.Generators.EventualConsistency;
            [GenerateEventualConsistencyMonitor(typeof(MissingKey))]
            public static partial class MissingConsistency;
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
            "EventualConsistencyMonitorGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Application.EventualConsistency.EventualConsistencyMonitor<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new EventualConsistencyMonitorGenerator(), out var run, out var updated);
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
