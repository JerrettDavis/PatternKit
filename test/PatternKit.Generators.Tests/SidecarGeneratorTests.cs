using Microsoft.CodeAnalysis;
using PatternKit.Cloud.Sidecar;
using PatternKit.Generators.Sidecar;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Sidecar generator")]
public sealed partial class SidecarGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates Sidecar factory")]
    [Fact]
    public Task Generates_Sidecar_Factory()
        => Given("a Sidecar declaration", () => Compile("""
            using PatternKit.Cloud.Sidecar;
            using PatternKit.Generators.Sidecar;
            namespace Demo;
            public sealed record OrderRequest(string OrderId);
            public sealed record OrderResponse(string Confirmation);
            [GenerateSidecar(typeof(OrderRequest), typeof(OrderResponse), FactoryMethodName = "Build", SidecarName = "order-sidecar")]
            public static partial class OrderSidecars
            {
                [SidecarBefore("trace")]
                private static void Trace(SidecarContext<OrderRequest> ctx) => ctx.Items["trace-id"] = "trace-1";
                [SidecarAfter("metrics")]
                private static void Metrics(SidecarContext<OrderRequest> ctx, OrderResponse response) => ctx.Items["confirmation"] = response.Confirmation;
                [SidecarHandler]
                private static OrderResponse Handle(SidecarContext<OrderRequest> ctx) => new(ctx.Request.OrderId);
            }
            """))
        .Then("the generated source creates the configured sidecar", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("Sidecar<global::Demo.OrderRequest, global::Demo.OrderResponse>.Create(\"order-sidecar\")", source);
            ScenarioExpect.Contains(".Before(\"trace\", Trace)", source);
            ScenarioExpect.Contains(".After(\"metrics\", Metrics)", source);
            ScenarioExpect.Contains(".Handle(Handle)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid Sidecar declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Sidecar_Declarations()
        => Given("invalid Sidecar declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.Sidecar;
                [GenerateSidecar(typeof(string), typeof(int))]
                public static class SidecarHost;
                """),
            Compile("""
                using PatternKit.Generators.Sidecar;
                [GenerateSidecar(typeof(string), typeof(int))]
                public static partial class SidecarHost;
                """),
            Compile("""
                using PatternKit.Cloud.Sidecar;
                using PatternKit.Generators.Sidecar;
                [GenerateSidecar(typeof(string), typeof(int))]
                public static partial class SidecarHost
                {
                    [SidecarBefore("trace")]
                    private static string Trace(SidecarContext<string> ctx) => ctx.Request;
                    [SidecarHandler]
                    private static int Handle(SidecarContext<string> ctx) => 1;
                }
                """),
            Compile("""
                using PatternKit.Cloud.Sidecar;
                using PatternKit.Generators.Sidecar;
                [GenerateSidecar(typeof(string), typeof(int))]
                public static partial class SidecarHost
                {
                    [SidecarBefore("trace")]
                    private static void Trace(SidecarContext<string> ctx) { }
                    [SidecarAfter("TRACE")]
                    private static void Metrics(SidecarContext<string> ctx, int response) { }
                    [SidecarHandler]
                    private static int Handle(SidecarContext<string> ctx) => 1;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            var ids = results.SelectMany(static result => result.Diagnostics.Select(static diagnostic => diagnostic.Id)).ToArray();
            ScenarioExpect.Contains(ids, static id => id == "PKSC001");
            ScenarioExpect.Contains(ids, static id => id == "PKSC002");
            ScenarioExpect.Contains(ids, static id => id == "PKSC003");
            ScenarioExpect.Contains(ids, static id => id == "PKSC004");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "SidecarGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(Sidecar<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new SidecarGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
