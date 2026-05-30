using Microsoft.CodeAnalysis;
using PatternKit.Generators.CacheStampedeProtection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Cache Stampede Protection generator")]
public sealed partial class CacheStampedeProtectionGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates cache stampede protection factory")]
    [Fact]
    public Task Generates_Cache_Stampede_Protection_Factory()
        => Given("a cache stampede protection declaration", () => Compile("""
            using PatternKit.Generators.CacheStampedeProtection;
            namespace Demo;
            [GenerateCacheStampedeProtection(typeof(string), FactoryMethodName = "Build", PolicyName = "catalog-single-flight")]
            public static partial class CatalogSingleFlight;
            """))
        .Then("the generated source creates the configured policy", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class CatalogSingleFlight", source);
            ScenarioExpect.Contains("CacheStampedeProtectionPolicy<string> Build()", source);
            ScenarioExpect.Contains("CacheStampedeProtectionPolicy<string>.Create(\"catalog-single-flight\").Build()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid cache stampede protection declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Cache_Stampede_Protection_Declarations()
        => Given("invalid cache stampede protection declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.CacheStampedeProtection;
                [GenerateCacheStampedeProtection(typeof(string))]
                public static class CatalogSingleFlight;
                """),
            Compile("""
                using PatternKit.Generators.CacheStampedeProtection;
                [GenerateCacheStampedeProtection(typeof(string), FactoryMethodName = "")]
                public static partial class CatalogSingleFlight;
                """),
            Compile("""
                using PatternKit.Generators.CacheStampedeProtection;
                [GenerateCacheStampedeProtection(typeof(string), PolicyName = "   ")]
                public static partial class CatalogSingleFlight;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKCSP001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKCSP002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKCSP002");
        })
        .AssertPassed();

    [Scenario("Generates cache stampede protection defaults and nested host wrappers")]
    [Fact]
    public Task Generates_Cache_Stampede_Protection_Defaults_And_Nested_Host_Wrappers()
        => Given("nested cache stampede protection declarations", () => Compile("""
            using PatternKit.Generators.CacheStampedeProtection;
            namespace Demo;
            public static partial class CatalogModule
            {
                internal abstract partial class Policies
                {
                    [GenerateCacheStampedeProtection(typeof(System.Guid), PolicyName = "single\\\"flight")]
                    private sealed partial class SingleFlight;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("public static partial class CatalogModule", source);
            ScenarioExpect.Contains("internal abstract partial class Policies", source);
            ScenarioExpect.Contains("private sealed partial class SingleFlight", source);
            ScenarioExpect.Contains("CacheStampedeProtectionPolicy<global::System.Guid>.Create(\"single\\\\\\\"flight\")", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates cache stampede protection factories for protected nested hosts")]
    [Fact]
    public Task Generates_Cache_Stampede_Protection_Factories_For_Protected_Nested_Hosts()
        => Given("protected nested cache stampede protection declarations", () => Compile("""
            using PatternKit.Generators.CacheStampedeProtection;
            namespace Demo;
            public abstract partial class CatalogModule
            {
                [GenerateCacheStampedeProtection(typeof(string), FactoryMethodName = "CreateProtected")]
                protected partial class ProtectedPolicy;

                [GenerateCacheStampedeProtection(typeof(string), FactoryMethodName = "CreatePrivateProtected")]
                private protected partial class PrivateProtectedPolicy;

                [GenerateCacheStampedeProtection(typeof(string), FactoryMethodName = "CreateProtectedInternal")]
                protected internal partial class ProtectedInternalPolicy;
            }
            """))
        .Then("generated sources preserve protected accessibility modifiers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = string.Join(Environment.NewLine, result.GeneratedSources);
            ScenarioExpect.Contains("protected partial class ProtectedPolicy", source);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedPolicy", source);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalPolicy", source);
            ScenarioExpect.Contains("CreateProtected()", source);
            ScenarioExpect.Contains("CreatePrivateProtected()", source);
            ScenarioExpect.Contains("CreateProtectedInternal()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips cache stampede protection generation for malformed result type")]
    [Fact]
    public Task Skips_Cache_Stampede_Protection_Generation_For_Malformed_Result_Type()
        => Given("a cache stampede protection declaration with an unresolved result type", () => Compile("""
            using PatternKit.Generators.CacheStampedeProtection;
            [GenerateCacheStampedeProtection(typeof(MissingResult))]
            public static partial class MissingSingleFlight;
            """))
        .Then("no generated source is produced by the generator", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Empty(result.GeneratedSources);
            ScenarioExpect.False(result.EmitSuccess);
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "CacheStampedeProtectionGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PatternKit.Cloud.CacheStampedeProtection.CacheStampedeProtectionPolicy<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new CacheStampedeProtectionGenerator(), out var run, out var updated);
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
