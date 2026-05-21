using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.PipesAndFilters;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Pipes and Filters generator")]
public sealed partial class PipesAndFiltersPipelineGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates pipes and filters pipeline builder factory")]
    [Fact]
    public Task Generates_Pipes_And_Filters_Pipeline_Builder_Factory()
        => Given("a pipes and filters pipeline declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            namespace Demo;
            public sealed record FulfillmentContext(string OrderId);
            [GeneratePipesAndFiltersPipeline(typeof(FulfillmentContext), FactoryMethodName = "Build", PipelineName = "fulfillment-pipeline")]
            public static partial class FulfillmentPipeline;
            """))
        .Then("the generated source creates the configured builder", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("PipesAndFiltersPipeline<global::Demo.FulfillmentContext>.Create(\"fulfillment-pipeline\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostic for non-partial pipes and filters declarations")]
    [Fact]
    public Task Reports_Diagnostic_For_Non_Partial_Pipes_And_Filters_Declarations()
        => Given("a non-partial pipes and filters declaration", () => Compile("""
            using PatternKit.Generators.Messaging;
            [GeneratePipesAndFiltersPipeline(typeof(string))]
            public static class PipelineHost;
            """))
        .Then("the diagnostic identifies the host", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKPF001"))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "PipesAndFiltersPipelineGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PipesAndFiltersPipeline<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new PipesAndFiltersPipelineGenerator(), out var run, out var updated);
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
