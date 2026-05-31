using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PatternKit.Generators.CacheAside;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class CacheAsidePolicyGeneratorTests
{
    [Scenario("Generates cache-aside policy factory")]
    [Fact]
    public void GeneratesCacheAsidePolicyFactory()
    {
        var source = """
            using PatternKit.Generators.CacheAside;

            namespace Demo;

            [GenerateCacheAsidePolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "products", TimeToLiveMilliseconds = 250)]
            public static partial class ProductCache
            {
                [CacheAsidePredicate]
                private static bool ShouldCache(string value) => value.Length > 0;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesCacheAsidePolicyFactory));
        var gen = new CacheAsidePolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Equal("ProductCache.CacheAsidePolicy.g.cs", generated.HintName);
        ScenarioExpect.Contains("Build()", text);
        ScenarioExpect.Contains("CacheAsidePolicy<string>.Create(\"products\")", text);
        ScenarioExpect.Contains("builder.WithTimeToLive(global::System.TimeSpan.FromMilliseconds(250));", text);
        ScenarioExpect.Contains("builder.CacheWhen(static value => ShouldCache(value));", text);

        var emit = updated.Emit(Stream.Null);
        ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
    }

    [Scenario("Reports diagnostic for non-partial cache-aside host")]
    [Fact]
    public void ReportsDiagnosticForNonPartialCacheAsideHost()
    {
        var source = """
            using PatternKit.Generators.CacheAside;

            namespace Demo;

            [GenerateCacheAsidePolicy(typeof(string))]
            public static class CacheAsideHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForNonPartialCacheAsideHost));

        ScenarioExpect.Equal("PKCA001", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid cache-aside configuration")]
    [Fact]
    public void ReportsDiagnosticForInvalidCacheAsideConfiguration()
    {
        var source = """
            using PatternKit.Generators.CacheAside;

            namespace Demo;

            [GenerateCacheAsidePolicy(typeof(string), TimeToLiveMilliseconds = -1)]
            public static partial class CacheAsideHost;
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidCacheAsideConfiguration));

        ScenarioExpect.Equal("PKCA002", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for invalid cache-aside predicate")]
    [Fact]
    public void ReportsDiagnosticForInvalidCacheAsidePredicate()
    {
        var source = """
            using PatternKit.Generators.CacheAside;

            namespace Demo;

            [GenerateCacheAsidePolicy(typeof(string))]
            public static partial class CacheAsideHost
            {
                [CacheAsidePredicate]
                private static string ShouldCache(string value) => value;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForInvalidCacheAsidePredicate));

        ScenarioExpect.Equal("PKCA003", diagnostic.Id);
    }

    [Scenario("Reports diagnostic for duplicate cache-aside predicates")]
    [Fact]
    public void ReportsDiagnosticForDuplicateCacheAsidePredicates()
    {
        var source = """
            using PatternKit.Generators.CacheAside;

            namespace Demo;

            [GenerateCacheAsidePolicy(typeof(string))]
            public static partial class CacheAsideHost
            {
                [CacheAsidePredicate]
                private static bool First(string value) => false;

                [CacheAsidePredicate]
                private static bool Second(string value) => true;
            }
            """;

        var diagnostic = RunAndGetSingleDiagnostic(source, nameof(ReportsDiagnosticForDuplicateCacheAsidePredicates));

        ScenarioExpect.Equal("PKCA004", diagnostic.Id);
    }

    [Scenario("Generates cache-aside policy factory for global struct host")]
    [Fact]
    public void GeneratesCacheAsidePolicyFactoryForGlobalStructHost()
    {
        var source = """
            using PatternKit.Generators.CacheAside;

            [GenerateCacheAsidePolicy(typeof(int), FactoryMethodName = "CreateNumbers", PolicyName = "numbers")]
            internal partial struct CacheAsideHost;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesCacheAsidePolicyFactoryForGlobalStructHost));
        var gen = new CacheAsidePolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = ScenarioExpect.Single(run.Results.SelectMany(result => result.GeneratedSources));
        var text = generated.SourceText.ToString();
        ScenarioExpect.Contains("internal partial struct CacheAsideHost", text);
        ScenarioExpect.Contains("CreateNumbers()", text);
        ScenarioExpect.Contains("builder.WithoutExpiration();", text);
        ScenarioExpect.DoesNotContain("namespace Demo;", text);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Generates cache-aside policy factory for abstract and sealed hosts")]
    [Fact]
    public void GeneratesCacheAsidePolicyFactoryForAbstractAndSealedHosts()
    {
        var source = """
            using PatternKit.Generators.CacheAside;

            namespace Demo;

            [GenerateCacheAsidePolicy(typeof(string), FactoryMethodName = "CreateAbstract")]
            public abstract partial class AbstractCacheAsideHost;

            [GenerateCacheAsidePolicy(typeof(string), FactoryMethodName = "CreateSealed")]
            public sealed partial class SealedCacheAsideHost;
            """;

        var comp = CreateCompilation(source, nameof(GeneratesCacheAsidePolicyFactoryForAbstractAndSealedHosts));
        var gen = new CacheAsidePolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generated = run.Results.SelectMany(result => result.GeneratedSources).ToArray();
        ScenarioExpect.Equal(2, generated.Length);
        var abstractText = ScenarioExpect.Single(generated.Where(source => source.HintName == "AbstractCacheAsideHost.CacheAsidePolicy.g.cs")).SourceText.ToString();
        var sealedText = ScenarioExpect.Single(generated.Where(source => source.HintName == "SealedCacheAsideHost.CacheAsidePolicy.g.cs")).SourceText.ToString();
        ScenarioExpect.Contains("public abstract partial class AbstractCacheAsideHost", abstractText);
        ScenarioExpect.Contains("CreateAbstract()", abstractText);
        ScenarioExpect.Contains("public sealed partial class SealedCacheAsideHost", sealedText);
        ScenarioExpect.Contains("CreateSealed()", sealedText);
        ScenarioExpect.True(updated.Emit(Stream.Null).Success);
    }

    [Scenario("Generates cache-aside policy source for nested accessibility variants")]
    [Fact]
    public void GeneratesCacheAsidePolicySourceForNestedAccessibilityVariants()
    {
        var source = """
            using PatternKit.Generators.CacheAside;

            namespace Demo;

            public partial class Outer
            {
                [GenerateCacheAsidePolicy(typeof(string), FactoryMethodName = "CreatePrivate")]
                private partial class PrivateCacheAsideHost;

                [GenerateCacheAsidePolicy(typeof(string), FactoryMethodName = "CreateProtected")]
                protected partial class ProtectedCacheAsideHost;

                [GenerateCacheAsidePolicy(typeof(string), FactoryMethodName = "CreateProtectedInternal")]
                protected internal partial class ProtectedInternalCacheAsideHost;

                [GenerateCacheAsidePolicy(typeof(string), FactoryMethodName = "CreatePrivateProtected")]
                private protected partial class PrivateProtectedCacheAsideHost;
            }
            """;

        var comp = CreateCompilation(source, nameof(GeneratesCacheAsidePolicySourceForNestedAccessibilityVariants));
        var gen = new CacheAsidePolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        ScenarioExpect.All(run.Results, result => ScenarioExpect.Empty(result.Diagnostics));
        var generatedText = string.Join("\n", run.Results.SelectMany(result => result.GeneratedSources).Select(source => source.SourceText.ToString()));
        ScenarioExpect.Contains("private partial class PrivateCacheAsideHost", generatedText);
        ScenarioExpect.Contains("protected partial class ProtectedCacheAsideHost", generatedText);
        ScenarioExpect.Contains("protected internal partial class ProtectedInternalCacheAsideHost", generatedText);
        ScenarioExpect.Contains("private protected partial class PrivateProtectedCacheAsideHost", generatedText);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
        => RoslynTestHelpers.CreateCompilation(
            source,
            assemblyName,
            extra:
            [
                MetadataReference.CreateFromFile(GetAbstractionsAssemblyPath()),
                MetadataReference.CreateFromFile(typeof(PatternKit.Cloud.CacheAside.CacheAsidePolicy<>).Assembly.Location)
            ]);

    private static string GetAbstractionsAssemblyPath()
        => Path.Combine(
            Path.GetDirectoryName(typeof(CacheAsidePolicyGenerator).Assembly.Location)!,
            "PatternKit.Generators.Abstractions.dll");

    private static Diagnostic RunAndGetSingleDiagnostic(string source, string assemblyName)
    {
        var comp = CreateCompilation(source, assemblyName);
        var gen = new CacheAsidePolicyGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);
        return ScenarioExpect.Single(run.Results.SelectMany(result => result.Diagnostics));
    }
}
