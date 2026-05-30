using Microsoft.CodeAnalysis;
using PatternKit.Generators.ReadWriteThroughCache;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class ReadWriteThroughCachePolicyGeneratorTests
{
    [Scenario("Generates read write through cache policy factory")]
    [Fact]
    public void Generates_ReadWriteThrough_Cache_Policy_Factory()
    {
        var result = Compile("""
            using PatternKit.Generators.ReadWriteThroughCache;
            namespace Demo;
            [GenerateReadWriteThroughCachePolicy(typeof(string), FactoryMethodName = "Build", PolicyName = "catalog-rwtc", TimeToLiveMilliseconds = 250)]
            public static partial class ProductCache;
            """);

        ScenarioExpect.Empty(result.Diagnostics);
        var source = ScenarioExpect.Single(result.GeneratedSources);
        ScenarioExpect.Contains("ReadWriteThroughCachePolicy<string> Build()", source);
        ScenarioExpect.Contains("ReadWriteThroughCachePolicy<string>.Create(\"catalog-rwtc\")", source);
        ScenarioExpect.Contains("WithTimeToLive(global::System.TimeSpan.FromMilliseconds(250))", source);
        ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
    }

    [Scenario("Generates read write through cache defaults and accessibility wrappers")]
    [Fact]
    public void Generates_ReadWriteThrough_Cache_Defaults_And_Accessibility_Wrappers()
    {
        var defaults = Compile("""
            using PatternKit.Generators.ReadWriteThroughCache;
            namespace Demo;
            [GenerateReadWriteThroughCachePolicy(typeof(int))]
            internal partial struct InternalCache;
            """);
        var accessibility = Compile("""
            using PatternKit.Generators.ReadWriteThroughCache;
            namespace Demo;
            public partial class CatalogModule
            {
                [GenerateReadWriteThroughCachePolicy(typeof(string), FactoryMethodName = "CreateAbstract")]
                public abstract partial class AbstractCache;
                [GenerateReadWriteThroughCachePolicy(typeof(string), FactoryMethodName = "CreateSealed")]
                private sealed partial class SealedCache;
                [GenerateReadWriteThroughCachePolicy(typeof(string), FactoryMethodName = "CreateProtected")]
                protected partial class ProtectedCache;
                [GenerateReadWriteThroughCachePolicy(typeof(string), FactoryMethodName = "CreatePrivateProtected")]
                private protected partial class PrivateProtectedCache;
                [GenerateReadWriteThroughCachePolicy(typeof(string), FactoryMethodName = "CreateProtectedInternal")]
                protected internal partial class ProtectedInternalCache;
            }
            """);

        ScenarioExpect.Empty(defaults.Diagnostics);
        ScenarioExpect.Contains("internal partial struct InternalCache", ScenarioExpect.Single(defaults.GeneratedSources));
        ScenarioExpect.Contains("WithoutExpiration()", ScenarioExpect.Single(defaults.GeneratedSources));

        var source = string.Join(Environment.NewLine, accessibility.GeneratedSources);
        ScenarioExpect.Contains("public abstract partial class AbstractCache", source);
        ScenarioExpect.Contains("private sealed partial class SealedCache", source);
        ScenarioExpect.Contains("protected partial class ProtectedCache", source);
        ScenarioExpect.Contains("private protected partial class PrivateProtectedCache", source);
        ScenarioExpect.Contains("protected internal partial class ProtectedInternalCache", source);
    }

    [Scenario("Reports diagnostics for invalid read write through cache declarations")]
    [Fact]
    public void Reports_Diagnostics_For_Invalid_ReadWriteThrough_Cache_Declarations()
    {
        var nonPartial = Compile("""
            using PatternKit.Generators.ReadWriteThroughCache;
            [GenerateReadWriteThroughCachePolicy(typeof(string))]
            public static class ProductCache;
            """);
        var invalid = Compile("""
            using PatternKit.Generators.ReadWriteThroughCache;
            [GenerateReadWriteThroughCachePolicy(typeof(string), FactoryMethodName = "", PolicyName = " ", TimeToLiveMilliseconds = -1)]
            public static partial class ProductCache;
            """);

        ScenarioExpect.Contains(nonPartial.Diagnostics, static diagnostic => diagnostic.Id == "PKRWTC001");
        ScenarioExpect.Contains(invalid.Diagnostics, static diagnostic => diagnostic.Id == "PKRWTC002");
    }

    [Scenario("Skips read write through cache generation for malformed result type")]
    [Fact]
    public void Skips_ReadWriteThrough_Cache_Generation_For_Malformed_Result_Type()
    {
        var result = Compile("""
            using PatternKit.Generators.ReadWriteThroughCache;
            [GenerateReadWriteThroughCachePolicy(typeof(MissingResult))]
            public static partial class ProductCache;
            """);

        ScenarioExpect.Empty(result.Diagnostics);
        ScenarioExpect.Empty(result.GeneratedSources);
        ScenarioExpect.False(result.EmitSuccess);
    }

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "ReadWriteThroughCachePolicyGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Cloud.ReadWriteThroughCache.ReadWriteThroughCachePolicy<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new ReadWriteThroughCachePolicyGenerator(), out var run, out var updated);
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
