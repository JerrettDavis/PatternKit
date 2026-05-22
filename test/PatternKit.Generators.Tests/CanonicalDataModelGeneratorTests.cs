using Microsoft.CodeAnalysis;
using PatternKit.EnterpriseIntegration.CanonicalDataModel;
using PatternKit.Generators.CanonicalDataModel;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Canonical Data Model generator")]
public sealed partial class CanonicalDataModelGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates canonical data model factory")]
    [Fact]
    public Task Generates_Canonical_Data_Model_Factory()
        => Given("a canonical data model declaration", () => Compile("""
            using PatternKit.Generators.CanonicalDataModel;
            namespace Demo;
            public sealed record PartnerOrder(string Id, decimal Amount);
            public sealed record CanonicalOrder(string OrderId, decimal Total);
            [GenerateCanonicalDataModel(typeof(PartnerOrder), typeof(CanonicalOrder), FactoryMethodName = "Build", ModelName = "commerce-orders", AdapterName = "partner")]
            public static partial class PartnerOrderCanonicalModel
            {
                [CanonicalDataModelMapper]
                private static CanonicalOrder Map(PartnerOrder order) => new(order.Id, order.Amount);
            }
            """))
        .Then("the generated source creates the configured model", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("CanonicalDataModel<global::Demo.CanonicalOrder>.Create(\"commerce-orders\")", source);
            ScenarioExpect.Contains(".From<global::Demo.PartnerOrder>(\"partner\", Map)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid canonical data model declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Canonical_Data_Model_Declarations()
        => Given("invalid canonical data model declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.CanonicalDataModel;
                [GenerateCanonicalDataModel(typeof(string), typeof(int))]
                public static class CanonicalHost;
                """),
            Compile("""
                using PatternKit.Generators.CanonicalDataModel;
                [GenerateCanonicalDataModel(typeof(string), typeof(int))]
                public static partial class CanonicalHost;
                """),
            Compile("""
                using PatternKit.Generators.CanonicalDataModel;
                [GenerateCanonicalDataModel(typeof(string), typeof(int))]
                public static partial class CanonicalHost
                {
                    [CanonicalDataModelMapper]
                    private static string Map(string value) => value;
                }
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKCDM001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKCDM002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKCDM003");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "CanonicalDataModelGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(CanonicalDataModel<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new CanonicalDataModelGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
