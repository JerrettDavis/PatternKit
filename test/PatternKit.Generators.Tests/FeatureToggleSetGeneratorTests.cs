using Microsoft.CodeAnalysis;
using PatternKit.Application.FeatureToggles;
using PatternKit.Generators.FeatureToggles;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Feature Toggle generator")]
public sealed partial class FeatureToggleSetGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits feature toggle set factory")]
    [Fact]
    public Task Generator_Emits_Feature_Toggle_Set_Factory()
        => Given("a valid feature toggle declaration", () => Compile("""
            using PatternKit.Generators.FeatureToggles;
            namespace Demo;
            public sealed record CheckoutContext(string Tenant, decimal Total);
            [GenerateFeatureToggleSet(typeof(CheckoutContext), FactoryName = "Build", SetName = "checkout")]
            public static partial class CheckoutToggles
            {
                [FeatureToggleRule("new-checkout")]
                private static bool IsBeta(CheckoutContext context) => context.Tenant == "beta";

                [FeatureToggleRule("fraud-review", DefaultEnabled = false)]
                private static bool IsLargeOrder(CheckoutContext context) => context.Total >= 500m;
            }
            """))
        .Then("generated source creates the toggle set", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("FeatureToggleSet<global::Demo.CheckoutContext>.Create(\"checkout\")", source);
            ScenarioExpect.Contains(".AddRule(\"new-checkout\", false, IsBeta)", source);
            ScenarioExpect.Contains(".AddRule(\"fraud-review\", false, IsLargeOrder)", source);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator reports invalid feature toggle declarations")]
    [Theory]
    [InlineData("public static class CheckoutToggles { [FeatureToggleRule(\"x\")] private static bool IsEnabled(CheckoutContext context) => true; }", "PKFT001")]
    [InlineData("public static partial class CheckoutToggles;", "PKFT002")]
    [InlineData("public static partial class CheckoutToggles { [FeatureToggleRule(\"x\")] private static string IsEnabled(CheckoutContext context) => \"yes\"; }", "PKFT003")]
    [InlineData("public static partial class CheckoutToggles { [FeatureToggleRule(\"x\")] private bool IsEnabled(CheckoutContext context) => true; }", "PKFT003")]
    public Task Generator_Reports_Invalid_Feature_Toggle_Declarations(string declaration, string diagnosticId)
        => Given("an invalid feature toggle declaration", () => Compile($$"""
            using PatternKit.Generators.FeatureToggles;
            public sealed record CheckoutContext(string Tenant, decimal Total);
            [GenerateFeatureToggleSet(typeof(CheckoutContext))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "FeatureToggleSetGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(FeatureToggleSet<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new FeatureToggleSetGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            string.Join("\n", emit.Diagnostics));
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        string EmitDiagnostics);
}
