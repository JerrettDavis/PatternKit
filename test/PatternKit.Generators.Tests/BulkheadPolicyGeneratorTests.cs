using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Bulkhead;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Bulkhead Policy generator")]
public sealed partial class BulkheadPolicyGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates bulkhead policy factory")]
    [Fact]
    public Task Generates_Bulkhead_Policy_Factory()
        => Given("a configured bulkhead policy declaration", () => Compile("""
            using PatternKit.Generators.Bulkhead;
            namespace Demo;
            [GenerateBulkheadPolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "fulfillment", MaxConcurrency = 4, MaxQueueLength = 8, QueueTimeoutMilliseconds = 250)]
            public static partial class FulfillmentBulkhead;
            """))
        .Then("generated source creates the configured policy", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("FulfillmentBulkhead.BulkheadPolicy.g.cs", source.HintName);
            ScenarioExpect.Contains("public static partial class FulfillmentBulkhead", source.Source);
            ScenarioExpect.Contains("Build()", source.Source);
            ScenarioExpect.Contains("BulkheadPolicy<string>.Create(\"fulfillment\")", source.Source);
            ScenarioExpect.Contains(".WithMaxConcurrency(4)", source.Source);
            ScenarioExpect.Contains(".WithMaxQueueLength(8)", source.Source);
            ScenarioExpect.Contains(".WithQueueTimeout(global::System.TimeSpan.FromMilliseconds(250))", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid bulkhead declarations")]
    [Theory]
    [InlineData("public static class BulkheadHost;", "PKBH001")]
    [InlineData("public static partial class BulkheadHost;", "PKBH002", "MaxConcurrency = 0")]
    [InlineData("public static partial class BulkheadHost;", "PKBH002", "MaxQueueLength = -1")]
    [InlineData("public static partial class BulkheadHost;", "PKBH002", "QueueTimeoutMilliseconds = -1")]
    public Task Reports_Diagnostics_For_Invalid_Bulkhead_Declarations(string declaration, string diagnosticId, string configuration = "")
        => Given("an invalid bulkhead policy declaration", () => Compile($$"""
            using PatternKit.Generators.Bulkhead;
            [GenerateBulkheadPolicy(typeof(string){{(string.IsNullOrWhiteSpace(configuration) ? "" : ", " + configuration)}})]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates bulkhead defaults and host shapes")]
    [Fact]
    public Task Generates_Bulkhead_Defaults_And_Host_Shapes()
        => Given("bulkhead policy declarations with default names and host shapes", () => Compile("""
            using PatternKit.Generators.Bulkhead;
            namespace Demo;

            [GenerateBulkheadPolicy(typeof(string))]
            internal abstract partial class AbstractBulkhead;

            [GenerateBulkheadPolicy(typeof(string), PolicyName = "tenant\\\"bulkhead")]
            public sealed partial class SealedBulkhead;

            [GenerateBulkheadPolicy(typeof(int))]
            internal partial struct StructBulkhead;
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("internal abstract partial class AbstractBulkhead", combined);
            ScenarioExpect.Contains("public sealed partial class SealedBulkhead", combined);
            ScenarioExpect.Contains("internal partial struct StructBulkhead", combined);
            ScenarioExpect.Contains("Create(\"bulkhead\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"bulkhead\")", combined);
            ScenarioExpect.Contains(".WithMaxConcurrency(8)", combined);
            ScenarioExpect.Contains(".WithMaxQueueLength(0)", combined);
            ScenarioExpect.Contains("FromMilliseconds(0)", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested bulkhead host wrappers")]
    [Fact]
    public Task Generates_Nested_Bulkhead_Host_Wrappers()
        => Given("nested bulkhead policy declarations", () => Compile("""
            using PatternKit.Generators.Bulkhead;
            namespace Demo;

            public partial class BulkheadContainer
            {
                private partial class PrivateHost
                {
                    [GenerateBulkheadPolicy(typeof(string))]
                    protected partial class ProtectedBulkhead;

                    [GenerateBulkheadPolicy(typeof(string))]
                    private protected partial class PrivateProtectedBulkhead;

                    [GenerateBulkheadPolicy(typeof(string))]
                    protected internal partial class ProtectedInternalBulkhead;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("public partial class BulkheadContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedBulkhead", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedBulkhead", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalBulkhead", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed bulkhead result type")]
    [Fact]
    public Task Skips_Malformed_Bulkhead_Result_Type()
        => Given("a bulkhead policy declaration with a null result type", () => Compile("""
            using PatternKit.Generators.Bulkhead;
            [GenerateBulkheadPolicy(null!)]
            public static partial class BulkheadHost;
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "BulkheadPolicyGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new BulkheadPolicyGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString())).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Cloud.Bulkhead.BulkheadPolicy<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(BulkheadPolicyGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
