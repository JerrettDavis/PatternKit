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
            })
            .AssertPassed();

    [Scenario("Generator emits internal struct factory in the global namespace")]
    [Fact]
    public Task Generator_Emits_Internal_Struct_Factory_In_The_Global_Namespace()
        => Given("a valid internal struct declaration", () => Compile("""
            using PatternKit.Generators.IdentityMap;
            public sealed record Order(string Id);
            [GenerateIdentityMap(typeof(Order), typeof(string))]
            internal partial struct OrderIdentityMap
            {
                [IdentityMapKeySelector]
                private static string SelectKey(Order order) => order.Id;
            }
            """))
            .Then("generated source keeps the declaration shape and default factory name", result =>
            {
                ScenarioExpect.Empty(result.Diagnostics);
                ScenarioExpect.Contains("internal partial struct OrderIdentityMap", ScenarioExpect.Single(result.GeneratedSources));
                ScenarioExpect.Contains("Create()", result.GeneratedSources[0]);
                ScenarioExpect.DoesNotContain("namespace", result.GeneratedSources[0]);
            })
            .AssertPassed();

    [Scenario("Generator reports invalid identity map declarations")]
    [Theory]
    [InlineData("public static class OrderIdentityMap { [IdentityMapKeySelector] private static string SelectKey(Order order) => order.Id; }", "PKIM001")]
    [InlineData("public static partial class OrderIdentityMap;", "PKIM002")]
    [InlineData("public static partial class OrderIdentityMap { [IdentityMapKeySelector] private static string SelectKey(Order order) => order.Id; [IdentityMapKeySelector] private static string SelectAlternate(Order order) => order.Id; }", "PKIM002")]
    [InlineData("public static partial class OrderIdentityMap { [IdentityMapKeySelector] private static int SelectKey(Order order) => 1; }", "PKIM003")]
    [InlineData("public static partial class OrderIdentityMap { [IdentityMapKeySelector] private string SelectKey(Order order) => order.Id; }", "PKIM003")]
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

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "IdentityMapGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(IdentityMap<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new IdentityMapGenerator(), out var run, out _);
        var result = run.Results.Single();
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources);
}
