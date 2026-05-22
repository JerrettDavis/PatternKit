using Microsoft.CodeAnalysis;
using PatternKit.Cloud.GatewayRouting;
using PatternKit.Generators.GatewayRouting;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Gateway Routing generator")]
public sealed partial class GatewayRoutingGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates Gateway Routing factory")]
    [Fact]
    public Task Generates_Gateway_Routing_Factory()
        => Given("a Gateway Routing declaration", () => Compile("""
            using PatternKit.Generators.GatewayRouting;
            namespace Demo;
            public sealed record GatewayRequest(string Path);
            public sealed record GatewayResponse(string Body);
            [GenerateGatewayRouting(typeof(GatewayRequest), typeof(GatewayResponse), FactoryMethodName = "Build", GatewayName = "product-gateway")]
            public static partial class ProductGateway
            {
                [GatewayRoute("inventory")]
                private static bool IsInventory(GatewayRequest request) => request.Path.StartsWith("/inventory/");
                [GatewayRouteHandler("inventory")]
                private static GatewayResponse Inventory(GatewayRequest request) => new("inventory");
                [GatewayRouteFallback("not-found")]
                private static GatewayResponse NotFound(GatewayRequest request) => new("fallback");
            }
            """))
        .Then("the generated source creates the configured router", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("GatewayRouting<global::Demo.GatewayRequest, global::Demo.GatewayResponse>.Create(\"product-gateway\")", source);
            ScenarioExpect.Contains(".Route(\"inventory\", IsInventory, Inventory)", source);
            ScenarioExpect.Contains(".Fallback(\"not-found\", NotFound)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid Gateway Routing declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Gateway_Routing_Declarations()
        => Given("invalid Gateway Routing declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.GatewayRouting;
                [GenerateGatewayRouting(typeof(string), typeof(int))]
                public static class GatewayHost;
                """),
            Compile("""
                using PatternKit.Generators.GatewayRouting;
                [GenerateGatewayRouting(typeof(string), typeof(int))]
                public static partial class GatewayHost;
                """),
            Compile("""
                using PatternKit.Generators.GatewayRouting;
                [GenerateGatewayRouting(typeof(string), typeof(int))]
                public static partial class GatewayHost
                {
                    [GatewayRoute("inventory")]
                    private static string InventoryRoute(string value) => value;
                    [GatewayRouteHandler("inventory")]
                    private static int Inventory(string value) => 1;
                    [GatewayRouteFallback]
                    private static int Fallback(string value) => 0;
                }
                """),
            Compile("""
                using PatternKit.Generators.GatewayRouting;
                [GenerateGatewayRouting(typeof(string), typeof(int))]
                public static partial class GatewayHost
                {
                    [GatewayRoute("inventory")]
                    private static bool InventoryRoute(string value) => true;
                    [GatewayRouteHandler("inventory")]
                    private static string Inventory(string value) => value;
                    [GatewayRouteFallback]
                    private static int Fallback(string value) => 0;
                }
                """),
            Compile("""
                using PatternKit.Generators.GatewayRouting;
                [GenerateGatewayRouting(typeof(string), typeof(int))]
                public static partial class GatewayHost
                {
                    [GatewayRoute("inventory")]
                    private static bool InventoryRoute(string value) => true;
                    [GatewayRouteHandler("inventory")]
                    private static int Inventory(string value) => 1;
                    [GatewayRouteFallback]
                    private static string Fallback(string value) => value;
                }
                """),
            Compile("""
                using PatternKit.Generators.GatewayRouting;
                [GenerateGatewayRouting(typeof(string), typeof(int))]
                public static partial class GatewayHost
                {
                    [GatewayRoute("inventory")]
                    private static bool InventoryRoute(string value) => true;
                    [GatewayRouteHandler("inventory")]
                    private static int Inventory(string value) => 1;
                    [GatewayRouteHandler("INVENTORY")]
                    private static int Inventory2(string value) => 2;
                    [GatewayRouteFallback]
                    private static int Fallback(string value) => 0;
                }
                """),
            Compile("""
                using PatternKit.Generators.GatewayRouting;
                [GenerateGatewayRouting(typeof(string), typeof(int))]
                public static partial class GatewayHost
                {
                    [GatewayRoute("inventory")]
                    private static bool InventoryRoute(string value) => true;
                    [GatewayRoute("INVENTORY")]
                    private static bool InventoryRoute2(string value) => true;
                    [GatewayRouteHandler("inventory")]
                    private static int Inventory(string value) => 1;
                    [GatewayRouteFallback]
                    private static int Fallback(string value) => 0;
                }
                """),
            Compile("""
                using PatternKit.Generators.GatewayRouting;
                [GenerateGatewayRouting(typeof(string), typeof(int))]
                public static partial class GatewayHost
                {
                    [GatewayRoute("inventory")]
                    private static bool InventoryRoute(string value) => true;
                    [GatewayRouteFallback]
                    private static int Fallback(string value) => 0;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            var ids = results.SelectMany(static result => result.Diagnostics.Select(static diagnostic => diagnostic.Id)).ToArray();
            ScenarioExpect.Contains(ids, static id => id == "PKGR001");
            ScenarioExpect.Contains(ids, static id => id == "PKGR002");
            ScenarioExpect.Contains(ids, static id => id == "PKGR003");
            ScenarioExpect.Contains(ids, static id => id == "PKGR004");
            ScenarioExpect.Contains(ids, static id => id == "PKGR005");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "GatewayRoutingGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(GatewayRouting<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new GatewayRoutingGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
