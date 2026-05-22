using Microsoft.CodeAnalysis;
using PatternKit.Cloud.Ambassador;
using PatternKit.Generators.Ambassador;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Ambassador generator")]
public sealed partial class AmbassadorGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates ambassador factory")]
    [Fact]
    public Task Generates_Ambassador_Factory()
        => Given("an ambassador declaration", () => Compile("""
            using PatternKit.Cloud.Ambassador;
            using PatternKit.Generators.Ambassador;
            namespace Demo;
            public sealed record InventoryRequest(string Sku, string Tenant);
            public sealed record InventoryResponse(string Sku, string Status);
            [GenerateAmbassador(typeof(InventoryRequest), typeof(InventoryResponse), FactoryMethodName = "Build", AmbassadorName = "inventory-ambassador")]
            public static partial class InventoryAmbassador
            {
                [AmbassadorTransform]
                private static InventoryRequest Normalize(InventoryRequest request) => request with { Sku = request.Sku.ToUpperInvariant() };
                [AmbassadorConnectionPolicy]
                private static bool CanConnect(InventoryRequest request) => request.Tenant != "blocked";
                [AmbassadorTelemetry("trace")]
                private static void Trace(AmbassadorContext<InventoryRequest> ctx) => ctx.Items["trace"] = ctx.Request.Tenant;
                [AmbassadorCall]
                private static InventoryResponse Call(AmbassadorContext<InventoryRequest> ctx) => new(ctx.Request.Sku, "available");
                [AmbassadorFallback]
                private static InventoryResponse Fallback(AmbassadorContext<InventoryRequest> ctx) => new(ctx.Request.Sku, "cached");
            }
            """))
        .Then("the generated source creates the configured ambassador", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("Ambassador<global::Demo.InventoryRequest, global::Demo.InventoryResponse>.Create(\"inventory-ambassador\")", source);
            ScenarioExpect.Contains(".Transform(Normalize)", source);
            ScenarioExpect.Contains(".ConnectionPolicy(CanConnect)", source);
            ScenarioExpect.Contains(".Telemetry(\"trace\", Trace)", source);
            ScenarioExpect.Contains(".Call(Call)", source);
            ScenarioExpect.Contains(".Fallback(Fallback)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid ambassador declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Ambassador_Declarations()
        => Given("invalid ambassador declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.Ambassador;
                [GenerateAmbassador(typeof(string), typeof(int))]
                public static class AmbassadorHost;
                """),
            Compile("""
                using PatternKit.Generators.Ambassador;
                [GenerateAmbassador(typeof(string), typeof(int))]
                public static partial class AmbassadorHost;
                """),
            Compile("""
                using PatternKit.Cloud.Ambassador;
                using PatternKit.Generators.Ambassador;
                [GenerateAmbassador(typeof(string), typeof(int))]
                public static partial class AmbassadorHost
                {
                    [AmbassadorCall]
                    private static string Call(AmbassadorContext<string> ctx) => "";
                }
                """),
            Compile("""
                using PatternKit.Cloud.Ambassador;
                using PatternKit.Generators.Ambassador;
                [GenerateAmbassador(typeof(string), typeof(int))]
                public static partial class AmbassadorHost
                {
                    [AmbassadorTelemetry("trace")]
                    private static void Trace(AmbassadorContext<string> ctx) { }
                    [AmbassadorTelemetry("TRACE")]
                    private static void Trace2(AmbassadorContext<string> ctx) { }
                    [AmbassadorCall]
                    private static int Call(AmbassadorContext<string> ctx) => 1;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKAMB001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKAMB002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKAMB003");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKAMB004");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "AmbassadorGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(Ambassador<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new AmbassadorGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
