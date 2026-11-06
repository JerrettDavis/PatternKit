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
    [Theory]
    [InlineData("public static class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF001")]
    [InlineData("public static partial class MigrationHost;", "PKSF002")]
    [InlineData("public static partial class MigrationHost { [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF002")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF002")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; }", "PKSF002")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static int One(string value) => 1; [StranglerFigLegacy] private static int Two(string value) => 2; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF002")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int One(string value) => 1; [StranglerFigModern] private static int Two(string value) => 2; }", "PKSF002")]
    [InlineData("public partial class MigrationHost { [StranglerFigRoute(\"modern\")] private bool Route(string value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF003")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static string Route(string value) => value; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF003")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route() => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF003")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(int value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF003")]
    [InlineData("public partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF003")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static string Legacy(string value) => value; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF003")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static int Legacy(int value) => value; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF003")]
    [InlineData("public partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private int Modern(string value) => 2; }", "PKSF003")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static string Modern(string value) => value; }", "PKSF003")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route(string value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(int value) => value; }", "PKSF003")]
    [InlineData("public static partial class MigrationHost { [StranglerFigRoute(\"modern\")] private static bool Route1(string value) => true; [StranglerFigRoute(\"MODERN\")] private static bool Route2(string value) => true; [StranglerFigLegacy] private static int Legacy(string value) => 1; [StranglerFigModern] private static int Modern(string value) => 2; }", "PKSF004")]
    public Task Reports_Diagnostics_For_Invalid_Strangler_Fig_Declarations(string declaration, string diagnosticId)
        => Given("an invalid Strangler Fig declaration", () => Compile($$"""
            using PatternKit.Generators.StranglerFig;
            [GenerateStranglerFig(typeof(string), typeof(int))]
            {{declaration}}
            """))
        .Then("diagnostics identify invalid declarations", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates Strangler Fig defaults and host shapes")]
    [Fact]
    public Task Generates_Strangler_Fig_Defaults_And_Host_Shapes()
        => Given("Strangler Fig declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Generators.StranglerFig;
            namespace Demo;
            public sealed record CheckoutRequest(string Tenant);
            public sealed record CheckoutResponse(string Confirmation);

            [GenerateStranglerFig(typeof(CheckoutRequest), typeof(CheckoutResponse))]
            internal abstract partial class AbstractMigration
            {
                [StranglerFigRoute("default")]
                private static bool Route(CheckoutRequest request) => true;
                [StranglerFigLegacy]
                private static CheckoutResponse Legacy(CheckoutRequest request) => new("legacy");
                [StranglerFigModern]
                private static CheckoutResponse Modern(CheckoutRequest request) => new("modern");
            }

            [GenerateStranglerFig(typeof(CheckoutRequest), typeof(CheckoutResponse), MigrationName = "tenant\\\"migration")]
            public sealed partial class SealedMigration
            {
                [StranglerFigRoute("tenant")]
                private static bool Route(CheckoutRequest request) => true;
                [StranglerFigLegacy]
                private static CheckoutResponse Legacy(CheckoutRequest request) => new("legacy");
                [StranglerFigModern]
                private static CheckoutResponse Modern(CheckoutRequest request) => new("modern");
            }

            [GenerateStranglerFig(typeof(CheckoutRequest), typeof(CheckoutResponse))]
            internal partial struct StructMigration
            {
                [StranglerFigRoute("struct")]
                private static bool Route(CheckoutRequest request) => true;
                [StranglerFigLegacy]
                private static CheckoutResponse Legacy(CheckoutRequest request) => new("legacy");
                [StranglerFigModern]
                private static CheckoutResponse Modern(CheckoutRequest request) => new("modern");
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractMigration", combined);
            ScenarioExpect.Contains("public sealed partial class SealedMigration", combined);
            ScenarioExpect.Contains("internal partial struct StructMigration", combined);
            ScenarioExpect.Contains("Create(\"strangler-fig\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"migration\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested Strangler Fig host wrappers")]
    [Fact]
    public Task Generates_Nested_Strangler_Fig_Host_Wrappers()
        => Given("nested Strangler Fig declarations", () => Compile("""
            using PatternKit.Generators.StranglerFig;
            namespace Demo;
            public sealed record CheckoutRequest(string Tenant);
            public sealed record CheckoutResponse(string Confirmation);

            public partial class MigrationContainer
            {
                private partial class PrivateHost
                {
                    [GenerateStranglerFig(typeof(CheckoutRequest), typeof(CheckoutResponse))]
                    protected partial class ProtectedMigration
                    {
                        [StranglerFigRoute("protected")]
                        private static bool Route(CheckoutRequest request) => true;
                        [StranglerFigLegacy]
                        private static CheckoutResponse Legacy(CheckoutRequest request) => new("legacy");
                        [StranglerFigModern]
                        private static CheckoutResponse Modern(CheckoutRequest request) => new("modern");
                    }

                    [GenerateStranglerFig(typeof(CheckoutRequest), typeof(CheckoutResponse))]
                    private protected partial class PrivateProtectedMigration
                    {
                        [StranglerFigRoute("private-protected")]
                        private static bool Route(CheckoutRequest request) => true;
                        [StranglerFigLegacy]
                        private static CheckoutResponse Legacy(CheckoutRequest request) => new("legacy");
                        [StranglerFigModern]
                        private static CheckoutResponse Modern(CheckoutRequest request) => new("modern");
                    }

                    [GenerateStranglerFig(typeof(CheckoutRequest), typeof(CheckoutResponse))]
                    protected internal partial class ProtectedInternalMigration
                    {
                        [StranglerFigRoute("protected-internal")]
                        private static bool Route(CheckoutRequest request) => true;
                        [StranglerFigLegacy]
                        private static CheckoutResponse Legacy(CheckoutRequest request) => new("legacy");
                        [StranglerFigModern]
                        private static CheckoutResponse Modern(CheckoutRequest request) => new("modern");
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class MigrationContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedMigration", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedMigration", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalMigration", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed Strangler Fig type arguments")]
    [Theory]
    [InlineData("null!", "typeof(CheckoutResponse)")]
    [InlineData("typeof(CheckoutRequest)", "null!")]
    public Task Skips_Malformed_Strangler_Fig_Type_Arguments(string requestType, string responseType)
        => Given("a Strangler Fig declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.StranglerFig;
            public sealed record CheckoutRequest(string Tenant);
            public sealed record CheckoutResponse(string Confirmation);
            [GenerateStranglerFig({{requestType}}, {{responseType}})]
            public static partial class CheckoutMigration
            {
                [StranglerFigRoute("modern")]
                private static bool Route(CheckoutRequest request) => true;
                [StranglerFigLegacy]
                private static CheckoutResponse Legacy(CheckoutRequest request) => new("legacy");
                [StranglerFigModern]
                private static CheckoutResponse Modern(CheckoutRequest request) => new("modern");
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
