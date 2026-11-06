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
    [InlineData("public static partial class CheckoutToggles { [FeatureToggleRule(\"x\")] private static bool IsEnabled() => true; }", "PKFT003")]
    [InlineData("public static partial class CheckoutToggles { [FeatureToggleRule(\"x\")] private static bool IsEnabled(string context) => true; }", "PKFT003")]
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

    [Scenario("Generator emits feature toggle defaults and host shapes")]
    [Fact]
    public Task Generator_Emits_Feature_Toggle_Defaults_And_Host_Shapes()
        => Given("feature toggle declarations with default names and host shapes", () => Compile("""
            using PatternKit.Generators.FeatureToggles;
            namespace Demo;
            public sealed record CheckoutContext(string Tenant, decimal Total);

            [GenerateFeatureToggleSet(typeof(CheckoutContext))]
            internal abstract partial class AbstractToggles
            {
                [FeatureToggleRule("enabled")]
                private static bool IsEnabled(CheckoutContext context) => true;
            }

            [GenerateFeatureToggleSet(typeof(CheckoutContext), SetName = "tenant\\\"toggles")]
            public sealed partial class SealedToggles
            {
                [FeatureToggleRule("beta", DefaultEnabled = true)]
                private static bool Beta(CheckoutContext context) => context.Tenant == "beta";
            }

            [GenerateFeatureToggleSet(typeof(CheckoutContext))]
            internal partial struct StructToggles
            {
                [FeatureToggleRule("large-order")]
                private static bool LargeOrder(CheckoutContext context) => context.Total >= 500m;
            }
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractToggles", combined);
            ScenarioExpect.Contains("public sealed partial class SealedToggles", combined);
            ScenarioExpect.Contains("internal partial struct StructToggles", combined);
            ScenarioExpect.Contains("Create(\"feature-toggles\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"toggles\")", combined);
            ScenarioExpect.Contains(".AddRule(\"enabled\", false, IsEnabled)", combined);
            ScenarioExpect.Contains(".AddRule(\"beta\", true, Beta)", combined);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator emits nested feature toggle host wrappers")]
    [Fact]
    public Task Generator_Emits_Nested_Feature_Toggle_Host_Wrappers()
        => Given("nested feature toggle declarations", () => Compile("""
            using PatternKit.Generators.FeatureToggles;
            namespace Demo;
            public sealed record CheckoutContext(string Tenant, decimal Total);

            public partial class ToggleContainer
            {
                private partial class PrivateHost
                {
                    [GenerateFeatureToggleSet(typeof(CheckoutContext))]
                    protected partial class ProtectedToggles
                    {
                        [FeatureToggleRule("protected")]
                        private static bool Protected(CheckoutContext context) => true;
                    }

                    [GenerateFeatureToggleSet(typeof(CheckoutContext))]
                    private protected partial class PrivateProtectedToggles
                    {
                        [FeatureToggleRule("private-protected")]
                        private static bool PrivateProtected(CheckoutContext context) => true;
                    }

                    [GenerateFeatureToggleSet(typeof(CheckoutContext))]
                    protected internal partial class ProtectedInternalToggles
                    {
                        [FeatureToggleRule("protected-internal")]
                        private static bool ProtectedInternal(CheckoutContext context) => true;
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class ToggleContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedToggles", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedToggles", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalToggles", combined);
            ScenarioExpect.True(result.EmitSuccess, result.EmitDiagnostics);
        })
        .AssertPassed();

    [Scenario("Generator skips malformed feature toggle context type")]
    [Fact]
    public Task Generator_Skips_Malformed_Feature_Toggle_Context_Type()
        => Given("a feature toggle declaration with a null context type", () => Compile("""
            using PatternKit.Generators.FeatureToggles;
            [GenerateFeatureToggleSet(null!)]
            public static partial class CheckoutToggles
            {
                [FeatureToggleRule("x")]
                private static bool IsEnabled(string context) => true;
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
