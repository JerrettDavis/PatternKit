using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.Bulkhead;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class BulkheadPolicyGeneratorTests
{
    [Scenario("Generates bulkhead policy factory")]
    [Fact]
    public void GeneratesBulkheadPolicyFactory()
    {
        var source = """
            using PatternKit.Generators.Bulkhead;

            namespace Demo;

            [GenerateBulkheadPolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "fulfillment", MaxConcurrency = 4, MaxQueueLength = 8, QueueTimeoutMilliseconds = 250)]
            public static partial class FulfillmentBulkhead;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesBulkheadPolicyFactory));
        var gen = new BulkheadPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("FulfillmentBulkhead.BulkheadPolicy.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("BulkheadPolicy<string>.Create(\"fulfillment\")", text);
        ScenarioExpect.Contains(".WithMaxConcurrency(4)", text);
        ScenarioExpect.Contains(".WithMaxQueueLength(8)", text);
        ScenarioExpect.Contains(".WithQueueTimeout(global::System.TimeSpan.FromMilliseconds(250))", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial bulkhead host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialBulkheadHost()
    {
        var source = """
            using PatternKit.Generators.Bulkhead;

            namespace Demo;

            [GenerateBulkheadPolicy(typeof(string))]
            public static class BulkheadHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialBulkheadHost));

        ScenarioExpect.Equal("PKBH001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid bulkhead configuration")]
    [Fact]
    public void ReportsDiagnosticForInvalidBulkheadConfiguration()
    {
        var source = """
            using PatternKit.Generators.Bulkhead;

            namespace Demo;

            [GenerateBulkheadPolicy(typeof(string), MaxConcurrency = 0)]
            public static partial class BulkheadHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidBulkheadConfiguration));

        ScenarioExpect.Equal("PKBH002", diagnostic.Id);
    }

    [Scenario("Generates bulkhead policy factory for global struct host")]
    [Fact]
    public void GeneratesBulkheadPolicyFactoryForGlobalStructHost()
    {
        var source = """
            using PatternKit.Generators.Bulkhead;

            [GenerateBulkheadPolicy(typeof(int), FactoryMethodName = "CreateNumbers", PolicyName = "numbers")]
            internal partial struct BulkheadHost;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesBulkheadPolicyFactoryForGlobalStructHost));
        var gen = new BulkheadPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("internal partial struct BulkheadHost", text);
        ScenarioExpect.Contains("CreateNumbers()", text);
        ScenarioExpect.DoesNotContain("namespace Demo;", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
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

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new BulkheadPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
