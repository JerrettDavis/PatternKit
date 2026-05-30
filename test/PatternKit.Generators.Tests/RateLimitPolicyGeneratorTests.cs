using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.RateLimiting;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Rate Limit Policy generator")]
public sealed partial class RateLimitPolicyGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates rate-limit policy factory")]
    [Fact]
    public Task Generates_Rate_Limit_Policy_Factory()
        => Given("a configured rate-limit policy declaration", () => Compile("""
            using PatternKit.Generators.RateLimiting;
            namespace Demo;
            [GenerateRateLimitPolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "tenant-search", PermitLimit = 2, WindowMilliseconds = 1000)]
            public static partial class SearchRateLimit;
            """))
        .Then("generated source creates the configured policy", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Equal("SearchRateLimit.RateLimitPolicy.g.cs", source.HintName);
            ScenarioExpect.Contains("public static partial class SearchRateLimit", source.Source);
            ScenarioExpect.Contains("Build()", source.Source);
            ScenarioExpect.Contains("RateLimitPolicy<string>.Create(\"tenant-search\")", source.Source);
            ScenarioExpect.Contains(".WithPermitLimit(2)", source.Source);
            ScenarioExpect.Contains(".WithWindow(global::System.TimeSpan.FromMilliseconds(1000))", source.Source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid rate-limit declarations")]
    [Theory]
    [InlineData("public static class RateLimitHost;", "PKRLT001")]
    [InlineData("public static partial class RateLimitHost;", "PKRLT002", "PermitLimit = 0")]
    [InlineData("public static partial class RateLimitHost;", "PKRLT002", "WindowMilliseconds = 0")]
    public Task Reports_Diagnostics_For_Invalid_Rate_Limit_Declarations(string declaration, string diagnosticId, string configuration = "")
        => Given("an invalid rate-limit policy declaration", () => Compile($$"""
            using PatternKit.Generators.RateLimiting;
            [GenerateRateLimitPolicy(typeof(string){{(string.IsNullOrWhiteSpace(configuration) ? "" : ", " + configuration)}})]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates rate-limit defaults and host shapes")]
    [Fact]
    public Task Generates_Rate_Limit_Defaults_And_Host_Shapes()
        => Given("rate-limit policy declarations with default names and host shapes", () => Compile("""
            using PatternKit.Generators.RateLimiting;
            namespace Demo;

            [GenerateRateLimitPolicy(typeof(string))]
            internal abstract partial class AbstractRateLimit;

            [GenerateRateLimitPolicy(typeof(string), PolicyName = "tenant\\\"search")]
            public sealed partial class SealedRateLimit;

            [GenerateRateLimitPolicy(typeof(int))]
            internal partial struct StructRateLimit;
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("internal abstract partial class AbstractRateLimit", combined);
            ScenarioExpect.Contains("public sealed partial class SealedRateLimit", combined);
            ScenarioExpect.Contains("internal partial struct StructRateLimit", combined);
            ScenarioExpect.Contains("Create(\"rate-limit\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"search\")", combined);
            ScenarioExpect.Contains(".WithPermitLimit(60)", combined);
            ScenarioExpect.Contains("FromMilliseconds(60000)", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested rate-limit host wrappers")]
    [Fact]
    public Task Generates_Nested_Rate_Limit_Host_Wrappers()
        => Given("nested rate-limit policy declarations", () => Compile("""
            using PatternKit.Generators.RateLimiting;
            namespace Demo;

            public partial class RateLimitContainer
            {
                private partial class PrivateHost
                {
                    [GenerateRateLimitPolicy(typeof(string))]
                    protected partial class ProtectedRateLimit;

                    [GenerateRateLimitPolicy(typeof(string))]
                    private protected partial class PrivateProtectedRateLimit;

                    [GenerateRateLimitPolicy(typeof(string))]
                    protected internal partial class ProtectedInternalRateLimit;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources.Select(static source => source.Source));
            ScenarioExpect.Contains("public partial class RateLimitContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedRateLimit", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedRateLimit", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalRateLimit", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed rate-limit result type")]
    [Fact]
    public Task Skips_Malformed_Rate_Limit_Result_Type()
        => Given("a rate-limit policy declaration with a null result type", () => Compile("""
            using PatternKit.Generators.RateLimiting;
            [GenerateRateLimitPolicy(null!)]
            public static partial class RateLimitHost;
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = CreateCompilation(source, "RateLimitPolicyGeneratorTests");
        _ = RoslynTestHelpers.Run(compilation, new RateLimitPolicyGenerator(), out var run, out var updated);
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
                MetadataReference.CreateFromFile(typeof(PatternKit.Cloud.RateLimiting.RateLimitPolicy<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(RateLimitPolicyGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<GeneratedSource> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);

    private sealed record GeneratedSource(string HintName, string Source);
}
