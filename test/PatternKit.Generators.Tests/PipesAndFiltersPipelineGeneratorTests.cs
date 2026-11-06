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

    [Scenario("Generates pipes and filters host defaults and type shapes")]
    [Fact]
    public Task Generates_Pipes_And_Filters_Host_Defaults_And_Type_Shapes()
        => Given("pipeline declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.Messaging;

            namespace Demo;

            public sealed record FulfillmentContext(string OrderId);

            [GeneratePipesAndFiltersPipeline(typeof(FulfillmentContext))]
            internal abstract partial class AbstractPipeline;

            [GeneratePipesAndFiltersPipeline(typeof(FulfillmentContext), PipelineName = "tenant\\\"pipeline")]
            public sealed partial class SealedPipeline;

            [GeneratePipesAndFiltersPipeline(typeof(FulfillmentContext))]
            internal partial struct StructPipeline;
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractPipeline", combined);
            ScenarioExpect.Contains("Create()", combined);
            ScenarioExpect.Contains("Create(\"pipes-and-filters\")", combined);
            ScenarioExpect.Contains("public sealed partial class SealedPipeline", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"pipeline\")", combined);
            ScenarioExpect.Contains("internal partial struct StructPipeline", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested pipes and filters host wrappers")]
    [Fact]
    public Task Generates_Nested_Pipes_And_Filters_Host_Wrappers()
        => Given("nested pipeline declarations with non-public accessibility", () => Compile("""
            using PatternKit.Generators.Messaging;

            namespace Demo;

            public sealed record FulfillmentContext(string OrderId);

            public partial class PipelineContainer
            {
                private partial class PrivateHost
                {
                    [GeneratePipesAndFiltersPipeline(typeof(FulfillmentContext))]
                    protected partial class ProtectedPipeline;

                    [GeneratePipesAndFiltersPipeline(typeof(FulfillmentContext))]
                    private protected partial class PrivateProtectedPipeline;

                    [GeneratePipesAndFiltersPipeline(typeof(FulfillmentContext))]
                    protected internal partial class ProtectedInternalPipeline;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class PipelineContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedPipeline", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedPipeline", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalPipeline", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed pipes and filters type arguments")]
    [Fact]
    public Task Skips_Malformed_Pipes_And_Filters_Type_Arguments()
        => Given("a pipeline declaration with a null context type", () => Compile("""
            using PatternKit.Generators.Messaging;

            [GeneratePipesAndFiltersPipeline(null!)]
            public static partial class BrokenPipeline;
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
