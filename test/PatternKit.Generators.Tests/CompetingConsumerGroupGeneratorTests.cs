using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.CompetingConsumers;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Competing Consumers generator")]
public sealed partial class CompetingConsumerGroupGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates competing consumer group builder factory")]
    [Fact]
    public Task Generates_Competing_Consumer_Group_Builder_Factory()
        => Given("a competing consumer group declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            namespace Demo;
            public sealed record FulfillmentWork(string OrderId);
            public sealed record FulfillmentResult(string OrderId, string Consumer);
            [GenerateCompetingConsumerGroup(typeof(FulfillmentWork), typeof(FulfillmentResult), FactoryMethodName = "Build", GroupName = "fulfillment-consumers", MaxConcurrentDeliveries = 4)]
            public static partial class FulfillmentConsumers;
            """))
        .Then("the generated source creates the configured builder", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("CompetingConsumerGroup<global::Demo.FulfillmentWork, global::Demo.FulfillmentResult>.Create(\"fulfillment-consumers\")", source);
            ScenarioExpect.Contains(".WithMaxConcurrentDeliveries(4)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid competing consumer declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Competing_Consumer_Declarations()
        => Given("invalid competing consumer declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.Messaging;
                [GenerateCompetingConsumerGroup(typeof(string), typeof(int))]
                public static class ConsumerHost;
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                [GenerateCompetingConsumerGroup(typeof(string), typeof(int), MaxConcurrentDeliveries = 0)]
                public static partial class ConsumerHost;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKCNS001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKCNS002");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "CompetingConsumerGroupGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(CompetingConsumerGroup<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new CompetingConsumerGroupGenerator(), out var run, out var updated);
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
