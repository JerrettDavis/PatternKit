using Microsoft.CodeAnalysis;
using PatternKit.Generators.SnapshotCheckpoints;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Snapshot Checkpoint Manager generator")]
public sealed partial class SnapshotCheckpointManagerGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates snapshot checkpoint manager factory")]
    [Fact]
    public Task Generates_Snapshot_Checkpoint_Manager_Factory()
        => Given("a snapshot checkpoint manager declaration", () => Compile("""
            using PatternKit.Generators.SnapshotCheckpoints;
            namespace Demo;
            public sealed record OrderSnapshot(string OrderId, decimal Total);
            [GenerateSnapshotCheckpointManager(typeof(string), typeof(OrderSnapshot), FactoryMethodName = "Build", ManagerName = "order-checkpoints")]
            public static partial class OrderCheckpoints;
            """))
        .Then("the generated source creates the configured manager", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class OrderCheckpoints", source);
            ScenarioExpect.Contains("SnapshotCheckpointManager<string, global::Demo.OrderSnapshot> Build()", source);
            ScenarioExpect.Contains("SnapshotCheckpointManager<string, global::Demo.OrderSnapshot>.Create(\"order-checkpoints\").Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid snapshot checkpoint manager declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Snapshot_Checkpoint_Manager_Declarations()
        => Given("invalid snapshot checkpoint manager declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.SnapshotCheckpoints;
                public sealed record OrderSnapshot(string OrderId);
                [GenerateSnapshotCheckpointManager(typeof(string), typeof(OrderSnapshot))]
                public static class OrderCheckpoints;
                """),
            Compile("""
                using PatternKit.Generators.SnapshotCheckpoints;
                public sealed record OrderSnapshot(string OrderId);
                [GenerateSnapshotCheckpointManager(typeof(string), typeof(OrderSnapshot), FactoryMethodName = "")]
                public static partial class OrderCheckpoints;
                """),
            Compile("""
                using PatternKit.Generators.SnapshotCheckpoints;
                public sealed record OrderSnapshot(string OrderId);
                [GenerateSnapshotCheckpointManager(typeof(string), typeof(OrderSnapshot), ManagerName = "   ")]
                public static partial class OrderCheckpoints;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKSCP001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKSCP002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKSCP002");
        })
        .AssertPassed();

    [Scenario("Generates snapshot checkpoint manager defaults and nested host wrappers")]
    [Fact]
    public Task Generates_Snapshot_Checkpoint_Manager_Defaults_And_Nested_Host_Wrappers()
        => Given("nested snapshot checkpoint manager declarations", () => Compile("""
            using PatternKit.Generators.SnapshotCheckpoints;
            namespace Demo;
            public sealed record OrderSnapshot(string OrderId);
            public static partial class FulfillmentModule
            {
                internal abstract partial class Checkpoints
                {
                    [GenerateSnapshotCheckpointManager(typeof(System.Guid), typeof(OrderSnapshot), ManagerName = "order\\\"checkpoints")]
                    private sealed partial class OrderCheckpoints;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class FulfillmentModule", source);
            ScenarioExpect.Contains("internal abstract partial class Checkpoints", source);
            ScenarioExpect.Contains("private sealed partial class OrderCheckpoints", source);
            ScenarioExpect.Contains("SnapshotCheckpointManager<global::System.Guid, global::Demo.OrderSnapshot>.Create(\"order\\\\\\\"checkpoints\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates snapshot checkpoint manager factory for global namespace struct host")]
    [Fact]
    public Task Generates_Snapshot_Checkpoint_Manager_Factory_For_Global_Namespace_Struct_Host()
        => Given("a struct snapshot checkpoint manager host without a namespace", () => Compile("""
            using PatternKit.Generators.SnapshotCheckpoints;
            public sealed record OrderSnapshot(string OrderId);
            [GenerateSnapshotCheckpointManager(typeof(int), typeof(OrderSnapshot))]
            internal partial struct OrderCheckpoints;
            """))
        .Then("the generated source preserves the struct host shape", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("internal partial struct OrderCheckpoints", source);
            ScenarioExpect.Contains("SnapshotCheckpointManager<int, global::OrderSnapshot> Create()", source);
            ScenarioExpect.Contains("SnapshotCheckpointManager<int, global::OrderSnapshot>.Create(\"snapshot-checkpoints\").Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips snapshot checkpoint manager generation for malformed types")]
    [Fact]
    public Task Skips_Snapshot_Checkpoint_Manager_Generation_For_Malformed_Types()
        => Given("snapshot checkpoint declarations with unresolved types", () => new[]
        {
            Compile("""
                using PatternKit.Generators.SnapshotCheckpoints;
                public sealed record OrderSnapshot(string OrderId);
                [GenerateSnapshotCheckpointManager(typeof(MissingKey), typeof(OrderSnapshot))]
                public static partial class MissingKeyCheckpoints;
                """),
            Compile("""
                using PatternKit.Generators.SnapshotCheckpoints;
                [GenerateSnapshotCheckpointManager(typeof(string), typeof(MissingSnapshot))]
                public static partial class MissingSnapshotCheckpoints;
                """)
        })
        .Then("no generated source is produced by the generator", results =>
        {
            ScenarioExpect.Empty(results[0].Diagnostics);
            ScenarioExpect.Empty(results[0].GeneratedSources);
            ScenarioExpect.False(results[0].EmitSuccess);
            ScenarioExpect.Empty(results[1].Diagnostics);
            ScenarioExpect.Empty(results[1].GeneratedSources);
            ScenarioExpect.False(results[1].EmitSuccess);
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "SnapshotCheckpointManagerGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Application.SnapshotCheckpoints.SnapshotCheckpointManager<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new SnapshotCheckpointManagerGenerator(), out var run, out var updated);
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
