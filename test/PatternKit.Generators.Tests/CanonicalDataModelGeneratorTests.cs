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
    [Theory]
    [InlineData("public static class CanonicalHost { [CanonicalDataModelMapper] private static int Map(string value) => value.Length; }", "PKCDM001")]
    [InlineData("public static partial class CanonicalHost;", "PKCDM002")]
    [InlineData("public static partial class CanonicalHost { [CanonicalDataModelMapper] private static int One(string value) => value.Length; [CanonicalDataModelMapper] private static int Two(string value) => value.Length; }", "PKCDM002")]
    [InlineData("public partial class CanonicalHost { [CanonicalDataModelMapper] private int Map(string value) => value.Length; }", "PKCDM003")]
    [InlineData("public static partial class CanonicalHost { [CanonicalDataModelMapper] private static string Map(string value) => value; }", "PKCDM003")]
    [InlineData("public static partial class CanonicalHost { [CanonicalDataModelMapper] private static int Map() => 1; }", "PKCDM003")]
    [InlineData("public static partial class CanonicalHost { [CanonicalDataModelMapper] private static int Map(string value, string tenant) => value.Length; }", "PKCDM003")]
    [InlineData("public static partial class CanonicalHost { [CanonicalDataModelMapper] private static int Map(int value) => value; }", "PKCDM003")]
    public Task Reports_Diagnostics_For_Invalid_Canonical_Data_Model_Declarations(string declaration, string expected)
        => Given("an invalid canonical data model declaration", () => Compile($$"""
            using PatternKit.Generators.CanonicalDataModel;
            [GenerateCanonicalDataModel(typeof(string), typeof(int))]
            {{declaration}}
            """))
        .Then("diagnostics identify the invalid declaration", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expected))
        .AssertPassed();

    [Scenario("Generates canonical data model defaults and host shapes")]
    [Fact]
    public Task Generates_Canonical_Data_Model_Defaults_And_Host_Shapes()
        => Given("canonical data model declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Generators.CanonicalDataModel;
            namespace Demo;
            public sealed record PartnerOrder(string Id);
            public sealed record CanonicalOrder(string OrderId);

            [GenerateCanonicalDataModel(typeof(PartnerOrder), typeof(CanonicalOrder))]
            internal abstract partial class AbstractCanonicalModel
            {
                [CanonicalDataModelMapper]
                private static CanonicalOrder Map(PartnerOrder order) => new(order.Id);
            }

            [GenerateCanonicalDataModel(typeof(PartnerOrder), typeof(CanonicalOrder), ModelName = "tenant\\\"orders", AdapterName = "partner\\adapter")]
            public sealed partial class SealedCanonicalModel
            {
                [CanonicalDataModelMapper]
                private static CanonicalOrder Map(PartnerOrder order) => new(order.Id);
            }

            [GenerateCanonicalDataModel(typeof(PartnerOrder), typeof(CanonicalOrder))]
            internal partial struct StructCanonicalModel
            {
                [CanonicalDataModelMapper]
                private static CanonicalOrder Map(PartnerOrder order) => new(order.Id);
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractCanonicalModel", combined);
            ScenarioExpect.Contains("public sealed partial class SealedCanonicalModel", combined);
            ScenarioExpect.Contains("internal partial struct StructCanonicalModel", combined);
            ScenarioExpect.Contains("Create(\"canonical-data-model\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"orders\")", combined);
            ScenarioExpect.Contains(".From<global::Demo.PartnerOrder>(\"partner\\\\adapter\", Map)", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested canonical data model host wrappers")]
    [Fact]
    public Task Generates_Nested_Canonical_Data_Model_Host_Wrappers()
        => Given("nested canonical data model declarations", () => Compile("""
            using PatternKit.Generators.CanonicalDataModel;
            namespace Demo;
            public sealed record PartnerOrder(string Id);
            public sealed record CanonicalOrder(string OrderId);

            public partial class CanonicalContainer
            {
                private partial class PrivateHost
                {
                    [GenerateCanonicalDataModel(typeof(PartnerOrder), typeof(CanonicalOrder))]
                    protected partial class ProtectedCanonicalModel
                    {
                        [CanonicalDataModelMapper]
                        private static CanonicalOrder Map(PartnerOrder order) => new(order.Id);
                    }

                    [GenerateCanonicalDataModel(typeof(PartnerOrder), typeof(CanonicalOrder))]
                    private protected partial class PrivateProtectedCanonicalModel
                    {
                        [CanonicalDataModelMapper]
                        private static CanonicalOrder Map(PartnerOrder order) => new(order.Id);
                    }

                    [GenerateCanonicalDataModel(typeof(PartnerOrder), typeof(CanonicalOrder))]
                    protected internal partial class ProtectedInternalCanonicalModel
                    {
                        [CanonicalDataModelMapper]
                        private static CanonicalOrder Map(PartnerOrder order) => new(order.Id);
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class CanonicalContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedCanonicalModel", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedCanonicalModel", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalCanonicalModel", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed canonical data model type arguments")]
    [Theory]
    [InlineData("null!", "typeof(CanonicalOrder)")]
    [InlineData("typeof(PartnerOrder)", "null!")]
    public Task Skips_Malformed_Canonical_Data_Model_Type_Arguments(string sourceType, string canonicalType)
        => Given("a canonical data model declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.CanonicalDataModel;
            public sealed record PartnerOrder(string Id);
            public sealed record CanonicalOrder(string OrderId);
            [GenerateCanonicalDataModel({{sourceType}}, {{canonicalType}})]
            public static partial class PartnerOrderCanonicalModel
            {
                [CanonicalDataModelMapper]
                private static CanonicalOrder Map(PartnerOrder order) => new(order.Id);
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
