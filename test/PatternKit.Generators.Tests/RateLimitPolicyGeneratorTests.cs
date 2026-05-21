using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.RateLimiting;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class RateLimitPolicyGeneratorTests
{
    [Scenario("Generates rate-limit policy factory")]
    [Fact]
    public void GeneratesRateLimitPolicyFactory()
    {
        var source = """
            using PatternKit.Generators.RateLimiting;

            namespace Demo;

            [GenerateRateLimitPolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "tenant-search", PermitLimit = 2, WindowMilliseconds = 1000)]
            public static partial class SearchRateLimit;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesRateLimitPolicyFactory));
        var gen = new RateLimitPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("SearchRateLimit.RateLimitPolicy.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("RateLimitPolicy<string>.Create(\"tenant-search\")", text);
        ScenarioExpect.Contains(".WithPermitLimit(2)", text);
        ScenarioExpect.Contains(".WithWindow(global::System.TimeSpan.FromMilliseconds(1000))", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial rate-limit host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialRateLimitHost()
    {
        var source = """
            using PatternKit.Generators.RateLimiting;

            namespace Demo;

            [GenerateRateLimitPolicy(typeof(string))]
            public static class RateLimitHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialRateLimitHost));

        ScenarioExpect.Equal("PKRLT001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid rate-limit configuration")]
    [Fact]
    public void ReportsDiagnosticForInvalidRateLimitConfiguration()
    {
        var source = """
            using PatternKit.Generators.RateLimiting;

            namespace Demo;

            [GenerateRateLimitPolicy(typeof(string), PermitLimit = 0)]
            public static partial class RateLimitHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidRateLimitConfiguration));

        ScenarioExpect.Equal("PKRLT002", diagnostic.Id);
    }

    [Scenario("Generates rate-limit policy factory for global struct host")]
    [Fact]
    public void GeneratesRateLimitPolicyFactoryForGlobalStructHost()
    {
        var source = """
            using PatternKit.Generators.RateLimiting;

            [GenerateRateLimitPolicy(typeof(int), FactoryMethodName = "CreateNumbers", PolicyName = "numbers")]
            internal partial struct RateLimitHost;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesRateLimitPolicyFactoryForGlobalStructHost));
        var gen = new RateLimitPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("internal partial struct RateLimitHost", text);
        ScenarioExpect.Contains("CreateNumbers()", text);
        ScenarioExpect.Contains(".WithPermitLimit(60)", text);
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
                MetadataReference.CreateFromFile(typeof(PatternKit.Cloud.RateLimiting.RateLimitPolicy<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(RateLimitPolicyGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new RateLimitPolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
