using Microsoft.CodeAnalysis;
using PatternKit.Application.IdentityMap;
using PatternKit.Generators.IdentityMap;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Identity Map generator")]
public sealed partial class IdentityMapGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits identity map factory")]
    [Fact]
    public Task Generator_Emits_Identity_Map_Factory()
        => Given("a valid identity map declaration", () => Compile("""
            using PatternKit.Generators.IdentityMap;
            namespace Demo;
            public sealed record Order(string Id);
            [GenerateIdentityMap(typeof(Order), typeof(string), FactoryName = "CreateMap")]
            public static partial class OrderIdentityMap
            {
                [IdentityMapKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }
            """))
            .Then("generated source creates the map with the selector", result =>
            {
                ScenarioExpect.Empty(result.Diagnostics);
                ScenarioExpect.Contains("CreateMap()", ScenarioExpect.Single(result.GeneratedSources));
                ScenarioExpect.Contains("IdentityMap<global::Demo.Order, string>.Create(SelectKey).Build()", result.GeneratedSources[0]);
                ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
            })
            .AssertPassed();

    [Scenario("Generator emits identity map defaults and host shapes")]
    [Fact]
    public Task Generator_Emits_Identity_Map_Defaults_And_Host_Shapes()
        => Given("valid identity map declarations with default names and host shapes", () => Compile("""
            using PatternKit.Generators.IdentityMap;
            namespace Demo;
            public sealed record Order(string Id);

            [GenerateIdentityMap(typeof(Order), typeof(string))]
            internal abstract partial class AbstractIdentityMap
            {
                [IdentityMapKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }

            [GenerateIdentityMap(typeof(Order), typeof(string))]
            public sealed partial class SealedIdentityMap
            {
                [IdentityMapKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }

            [GenerateIdentityMap(typeof(Order), typeof(string))]
            internal partial struct StructIdentityMap
            {
                [IdentityMapKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }
            """))
            .Then("generated source keeps declaration shapes and default factory names", result =>
            {
                ScenarioExpect.Empty(result.Diagnostics);
                ScenarioExpect.Equal(3, result.GeneratedSources.Count);

                var combined = string.Join("\n", result.GeneratedSources);
                ScenarioExpect.Contains("internal abstract partial class AbstractIdentityMap", combined);
                ScenarioExpect.Contains("public sealed partial class SealedIdentityMap", combined);
                ScenarioExpect.Contains("internal partial struct StructIdentityMap", combined);
                ScenarioExpect.Contains("Create()", combined);
                ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
            })
            .AssertPassed();

    [Scenario("Generator reports invalid identity map declarations")]
    [Theory]
    [InlineData("public static class OrderIdentityMap { [IdentityMapKeySelector] private static string SelectKey(Order order) => order.Id; }", "PKIM001")]
    [InlineData("public static partial class OrderIdentityMap;", "PKIM002")]
    [InlineData("public static partial class OrderIdentityMap { [IdentityMapKeySelector] private static string SelectKey(Order order) => order.Id; [IdentityMapKeySelector] private static string SelectAlternate(Order order) => order.Id; }", "PKIM002")]
    [InlineData("public static partial class OrderIdentityMap { [IdentityMapKeySelector] private static int SelectKey(Order order) => 1; }", "PKIM003")]
    [InlineData("public static partial class OrderIdentityMap { [IdentityMapKeySelector] private string SelectKey(Order order) => order.Id; }", "PKIM003")]
    [InlineData("public static partial class OrderIdentityMap { [IdentityMapKeySelector] private static string SelectKey() => string.Empty; }", "PKIM003")]
    [InlineData("public static partial class OrderIdentityMap { [IdentityMapKeySelector] private static string SelectKey(string order) => order; }", "PKIM003")]
    public Task Generator_Reports_Invalid_Identity_Map_Declarations(string declaration, string diagnosticId)
        => Given("an invalid identity map declaration", () => Compile($$"""
            using PatternKit.Generators.IdentityMap;
            public sealed record Order(string Id);
            [GenerateIdentityMap(typeof(Order), typeof(string))]
            {{declaration}}
            """))
            .Then("the expected diagnostic is reported", result =>
                ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
            .AssertPassed();

    [Scenario("Generator emits nested identity map host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Identity_Map_Host_Wrappers()
        => Given("nested identity map declarations", () => Compile("""
            using PatternKit.Generators.IdentityMap;
            namespace Demo;
            public sealed record Order(string Id);

            public partial class IdentityMapContainer
            {
                private partial class PrivateHost
                {
                    [GenerateIdentityMap(typeof(Order), typeof(string))]
                    protected partial class ProtectedIdentityMap
                    {
                        [IdentityMapKeySelector]
                        private static string SelectKey(Order order) => order.Id;
                    }

                    [GenerateIdentityMap(typeof(Order), typeof(string))]
                    private protected partial class PrivateProtectedIdentityMap
                    {
                        [IdentityMapKeySelector]
                        private static string SelectKey(Order order) => order.Id;
                    }

                    [GenerateIdentityMap(typeof(Order), typeof(string))]
                    protected internal partial class ProtectedInternalIdentityMap
                    {
                        [IdentityMapKeySelector]
                        private static string SelectKey(Order order) => order.Id;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class IdentityMapContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedIdentityMap", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedIdentityMap", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalIdentityMap", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generator skips malformed identity map type arguments")]
    [Theory]
    [InlineData("null!", "typeof(string)")]
    [InlineData("typeof(Order)", "null!")]
    public Task Generator_Skips_Malformed_Identity_Map_Type_Arguments(string entityType, string keyType)
        => Given("an identity map declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.IdentityMap;
            public sealed record Order(string Id);
            [GenerateIdentityMap({{entityType}}, {{keyType}})]
            public static partial class OrderIdentityMap
            {
                [IdentityMapKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "IdentityMapGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(IdentityMap<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new IdentityMapGenerator(), out var run, out var updated);
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
