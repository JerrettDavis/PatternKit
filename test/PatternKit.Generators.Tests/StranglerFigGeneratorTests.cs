using Microsoft.CodeAnalysis;
using PatternKit.Cloud.StranglerFig;
using PatternKit.Generators.StranglerFig;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Strangler Fig generator")]
public sealed partial class StranglerFigGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates Strangler Fig factory")]
    [Fact]
    public Task Generates_Strangler_Fig_Factory()
        => Given("a Strangler Fig declaration", () => Compile("""
            using PatternKit.Generators.StranglerFig;
            namespace Demo;
            public sealed record CheckoutRequest(string Tenant, string OrderId);
            public sealed record CheckoutResponse(string Confirmation);
            [GenerateStranglerFig(typeof(CheckoutRequest), typeof(CheckoutResponse), FactoryMethodName = "Build", MigrationName = "checkout-migration")]
            public static partial class CheckoutMigration
            {
                [StranglerFigRoute("enterprise-cutover")]
                private static bool IsEnterprise(CheckoutRequest request) => request.Tenant == "enterprise";
                [StranglerFigLegacy]
                private static CheckoutResponse Legacy(CheckoutRequest request) => new("legacy:" + request.OrderId);
                [StranglerFigModern]
                private static CheckoutResponse Modern(CheckoutRequest request) => new("modern:" + request.OrderId);
            }
            """))
        .Then("the generated source creates the configured migration", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("StranglerFig<global::Demo.CheckoutRequest, global::Demo.CheckoutResponse>.Create(\"checkout-migration\")", source);
            ScenarioExpect.Contains(".RouteToModern(\"enterprise-cutover\", IsEnterprise)", source);
            ScenarioExpect.Contains(".Legacy(Legacy)", source);
            ScenarioExpect.Contains(".Modern(Modern)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid Strangler Fig declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Strangler_Fig_Declarations()
        => Given("invalid Strangler Fig declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.StranglerFig;
                [GenerateStranglerFig(typeof(string), typeof(int))]
                public static class MigrationHost;
                """),
            Compile("""
                using PatternKit.Generators.StranglerFig;
                [GenerateStranglerFig(typeof(string), typeof(int))]
                public static partial class MigrationHost;
                """),
            Compile("""
                using PatternKit.Generators.StranglerFig;
                [GenerateStranglerFig(typeof(string), typeof(int))]
                public static partial class MigrationHost
                {
                    [StranglerFigRoute("modern")]
                    private static string ModernRoute(string value) => value;
                    [StranglerFigLegacy]
                    private static int Legacy(string value) => 1;
                    [StranglerFigModern]
                    private static int Modern(string value) => 2;
                }
                """),
            Compile("""
                using PatternKit.Generators.StranglerFig;
                [GenerateStranglerFig(typeof(string), typeof(int))]
                public static partial class MigrationHost
                {
                    [StranglerFigRoute("modern")]
                    private static bool Route1(string value) => true;
                    [StranglerFigRoute("MODERN")]
                    private static bool Route2(string value) => true;
                    [StranglerFigLegacy]
                    private static int Legacy(string value) => 1;
                    [StranglerFigModern]
                    private static int Modern(string value) => 2;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKSF001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKSF002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKSF003");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKSF004");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "StranglerFigGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(StranglerFig<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new StranglerFigGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
