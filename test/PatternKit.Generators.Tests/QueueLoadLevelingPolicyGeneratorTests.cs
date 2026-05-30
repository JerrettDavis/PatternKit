using Microsoft.CodeAnalysis;
using PatternKit.Cloud.QueueLoadLeveling;
using PatternKit.Generators.QueueLoadLeveling;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Queue Load Leveling generator")]
public sealed partial class QueueLoadLevelingPolicyGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates queue load leveling policy factory")]
    [Fact]
    public Task Generates_Queue_Load_Leveling_Policy_Factory()
        => Given("a queue load leveling policy declaration", () => Compile("""
            using PatternKit.Generators.QueueLoadLeveling;
            namespace Demo;
            public sealed record FulfillmentResult(string OrderId);
            [GenerateQueueLoadLevelingPolicy(typeof(FulfillmentResult), FactoryMethodName = "Build", PolicyName = "fulfillment-queue", MaxConcurrentWorkers = 2, MaxQueueLength = 8, QueueTimeoutMilliseconds = 250)]
            public static partial class FulfillmentQueue;
            """))
        .Then("the generated source creates the configured policy", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("QueueLoadLevelingPolicy<global::Demo.FulfillmentResult>.Create(\"fulfillment-queue\")", source);
            ScenarioExpect.Contains(".WithMaxConcurrentWorkers(2)", source);
            ScenarioExpect.Contains(".WithMaxQueueLength(8)", source);
            ScenarioExpect.Contains(".WithQueueTimeout(global::System.TimeSpan.FromMilliseconds(250))", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostic for invalid queue load leveling declarations")]
    [Fact]
    public Task Reports_Diagnostic_For_Invalid_Queue_Load_Leveling_Declarations()
        => Given("invalid queue load leveling declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.QueueLoadLeveling;
                [GenerateQueueLoadLevelingPolicy(typeof(string))]
                public static class QueueHost;
                """),
            Compile("""
                using PatternKit.Generators.QueueLoadLeveling;
                [GenerateQueueLoadLevelingPolicy(typeof(string), MaxConcurrentWorkers = 0)]
                public static partial class QueueHost;
                """),
            Compile("""
                using PatternKit.Generators.QueueLoadLeveling;
                [GenerateQueueLoadLevelingPolicy(typeof(string), MaxQueueLength = -1)]
                public static partial class QueueHost;
                """),
            Compile("""
                using PatternKit.Generators.QueueLoadLeveling;
                [GenerateQueueLoadLevelingPolicy(typeof(string), QueueTimeoutMilliseconds = -1)]
                public static partial class QueueHost;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKQL001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKQL002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKQL002");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKQL002");
        })
        .AssertPassed();

    [Scenario("Generates queue load leveling policies with defaults and escaped names")]
    [Fact]
    public Task Generates_Queue_Load_Leveling_Policies_With_Defaults_And_Escaped_Names()
        => Given("queue load leveling declarations with generator defaults", () => new[]
        {
            Compile("""
                using PatternKit.Generators.QueueLoadLeveling;
                [GenerateQueueLoadLevelingPolicy(typeof(string))]
                internal partial struct QueueDefaults;
                """),
            Compile("""
                using PatternKit.Generators.QueueLoadLeveling;
                namespace Demo;
                [GenerateQueueLoadLevelingPolicy(typeof(string), PolicyName = "queue\"" + "\\level")]
                public abstract partial class EscapedQueue;
                """)
        })
        .Then("the generated sources preserve host shape and configured names", results =>
        {
            var defaultSource = ScenarioExpect.Single(results[0].GeneratedSources);
            ScenarioExpect.Empty(results[0].Diagnostics);
            ScenarioExpect.Contains("internal partial struct QueueDefaults", defaultSource);
            ScenarioExpect.Contains("Create()", defaultSource);
            ScenarioExpect.Contains("QueueLoadLevelingPolicy<string>.Create(\"queue-load-leveling\")", defaultSource);
            ScenarioExpect.Contains(".WithMaxConcurrentWorkers(1)", defaultSource);
            ScenarioExpect.Contains(".WithMaxQueueLength(100)", defaultSource);
            ScenarioExpect.Contains(".WithQueueTimeout(global::System.TimeSpan.FromMilliseconds(30000))", defaultSource);
            ScenarioExpect.True(results[0].EmitSuccess, string.Join(Environment.NewLine, results[0].EmitDiagnostics));

            var escapedSource = ScenarioExpect.Single(results[1].GeneratedSources);
            ScenarioExpect.Empty(results[1].Diagnostics);
            ScenarioExpect.Contains("namespace Demo;", escapedSource);
            ScenarioExpect.Contains("public abstract partial class EscapedQueue", escapedSource);
            ScenarioExpect.Contains("Create(\"queue\\\"\\\\level\")", escapedSource);
            ScenarioExpect.True(results[1].EmitSuccess, string.Join(Environment.NewLine, results[1].EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates queue load leveling policy inside nested hosts")]
    [Fact]
    public Task Generates_Queue_Load_Leveling_Policy_Inside_Nested_Hosts()
        => Given("a nested queue load leveling declaration", () => Compile("""
            using PatternKit.Generators.QueueLoadLeveling;
            namespace Demo;
            public static partial class FulfillmentModule
            {
                internal abstract partial class Queues
                {
                    [GenerateQueueLoadLevelingPolicy(typeof(string))]
                    private sealed partial class WorkQueue;
                }
            }
            """))
        .Then("the generated source recreates each containing partial type", result =>
        {
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Contains("public static partial class FulfillmentModule", source);
            ScenarioExpect.Contains("internal abstract partial class Queues", source);
            ScenarioExpect.Contains("private sealed partial class WorkQueue", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips queue load leveling generation for malformed result type")]
    [Fact]
    public Task Skips_Queue_Load_Leveling_Generation_For_Malformed_Result_Type()
        => Given("a queue load leveling declaration with an unresolved result type", () => Compile("""
            using PatternKit.Generators.QueueLoadLeveling;
            [GenerateQueueLoadLevelingPolicy(typeof(MissingResult))]
            public static partial class MissingQueue;
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
            "QueueLoadLevelingPolicyGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(QueueLoadLevelingPolicy<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new QueueLoadLevelingPolicyGenerator(), out var run, out var updated);
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
