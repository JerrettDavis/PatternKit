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
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKQL001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKQL002");
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
