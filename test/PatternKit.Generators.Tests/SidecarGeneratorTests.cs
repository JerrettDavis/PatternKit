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
    [Theory]
    [InlineData("public static class SidecarHost { [SidecarBefore(\"trace\")] private static void Trace(SidecarContext<string> ctx) { } [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC001")]
    [InlineData("public static partial class SidecarHost;", "PKSC002")]
    [InlineData("public static partial class SidecarHost { [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC002")]
    [InlineData("public static partial class SidecarHost { [SidecarBefore(\"trace\")] private static void Trace(SidecarContext<string> ctx) { } }", "PKSC002")]
    [InlineData("public static partial class SidecarHost { [SidecarBefore(\"trace\")] private static void Trace(SidecarContext<string> ctx) { } [SidecarHandler] private static int One(SidecarContext<string> ctx) => 1; [SidecarHandler] private static int Two(SidecarContext<string> ctx) => 2; }", "PKSC002")]
    [InlineData("public static partial class SidecarHost { [SidecarBefore(\"trace\")] private static void Trace(SidecarContext<string> ctx) { } [SidecarAfter(\"TRACE\")] private static void Metrics(SidecarContext<string> ctx, int response) { } [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC004")]
    [InlineData("public partial class SidecarHost { [SidecarBefore(\"trace\")] private void Trace(SidecarContext<string> ctx) { } [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC003")]
    [InlineData("public static partial class SidecarHost { [SidecarBefore(\"trace\")] private static string Trace(SidecarContext<string> ctx) => ctx.Request; [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC003")]
    [InlineData("public static partial class SidecarHost { [SidecarBefore(\"trace\")] private static void Trace() { } [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC003")]
    [InlineData("public static partial class SidecarHost { [SidecarBefore(\"trace\")] private static void Trace(SidecarContext<int> ctx) { } [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC003")]
    [InlineData("public partial class SidecarHost { [SidecarAfter(\"metrics\")] private void Metrics(SidecarContext<string> ctx, int response) { } [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC003")]
    [InlineData("public static partial class SidecarHost { [SidecarAfter(\"metrics\")] private static string Metrics(SidecarContext<string> ctx, int response) => string.Empty; [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC003")]
    [InlineData("public static partial class SidecarHost { [SidecarAfter(\"metrics\")] private static void Metrics(SidecarContext<string> ctx) { } [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC003")]
    [InlineData("public static partial class SidecarHost { [SidecarAfter(\"metrics\")] private static void Metrics(SidecarContext<string> ctx, string response) { } [SidecarHandler] private static int Handle(SidecarContext<string> ctx) => 1; }", "PKSC003")]
    [InlineData("public static partial class SidecarHost { [SidecarBefore(\"trace\")] private static void Trace(SidecarContext<string> ctx) { } [SidecarHandler] private static string Handle(SidecarContext<string> ctx) => string.Empty; }", "PKSC003")]
    [InlineData("public static partial class SidecarHost { [SidecarBefore(\"trace\")] private static void Trace(SidecarContext<string> ctx) { } [SidecarHandler] private static int Handle(string ctx) => 1; }", "PKSC003")]
    public Task Reports_Diagnostics_For_Invalid_Sidecar_Declarations(string declaration, string diagnosticId)
        => Given("an invalid Sidecar declaration", () => Compile($$"""
            using PatternKit.Cloud.Sidecar;
            using PatternKit.Generators.Sidecar;
            [GenerateSidecar(typeof(string), typeof(int))]
            {{declaration}}
            """))
        .Then("diagnostics identify invalid declarations", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates Sidecar defaults and host shapes")]
    [Fact]
    public Task Generates_Sidecar_Defaults_And_Host_Shapes()
        => Given("Sidecar declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Cloud.Sidecar;
            using PatternKit.Generators.Sidecar;
            namespace Demo;
            public sealed record OrderRequest(string OrderId);
            public sealed record OrderResponse(string Confirmation);

            [GenerateSidecar(typeof(OrderRequest), typeof(OrderResponse))]
            internal abstract partial class AbstractSidecar
            {
                [SidecarBefore("trace")]
                private static void Trace(SidecarContext<OrderRequest> ctx) { }
                [SidecarHandler]
                private static OrderResponse Handle(SidecarContext<OrderRequest> ctx) => new(ctx.Request.OrderId);
            }

            [GenerateSidecar(typeof(OrderRequest), typeof(OrderResponse), SidecarName = "tenant\\\"sidecar")]
            public sealed partial class SealedSidecar
            {
                [SidecarAfter("metrics")]
                private static void Metrics(SidecarContext<OrderRequest> ctx, OrderResponse response) { }
                [SidecarHandler]
                private static OrderResponse Handle(SidecarContext<OrderRequest> ctx) => new(ctx.Request.OrderId);
            }

            [GenerateSidecar(typeof(OrderRequest), typeof(OrderResponse))]
            internal partial struct StructSidecar
            {
                [SidecarBefore("trace")]
                private static void Trace(SidecarContext<OrderRequest> ctx) { }
                [SidecarHandler]
                private static OrderResponse Handle(SidecarContext<OrderRequest> ctx) => new(ctx.Request.OrderId);
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractSidecar", combined);
            ScenarioExpect.Contains("public sealed partial class SealedSidecar", combined);
            ScenarioExpect.Contains("internal partial struct StructSidecar", combined);
            ScenarioExpect.Contains("Create(\"sidecar\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"sidecar\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested Sidecar host wrappers")]
    [Fact]
    public Task Generates_Nested_Sidecar_Host_Wrappers()
        => Given("nested Sidecar declarations", () => Compile("""
            using PatternKit.Cloud.Sidecar;
            using PatternKit.Generators.Sidecar;
            namespace Demo;
            public sealed record OrderRequest(string OrderId);
            public sealed record OrderResponse(string Confirmation);

            public partial class SidecarContainer
            {
                private partial class PrivateHost
                {
                    [GenerateSidecar(typeof(OrderRequest), typeof(OrderResponse))]
                    protected partial class ProtectedSidecar
                    {
                        [SidecarBefore("trace")]
                        private static void Trace(SidecarContext<OrderRequest> ctx) { }
                        [SidecarHandler]
                        private static OrderResponse Handle(SidecarContext<OrderRequest> ctx) => new(ctx.Request.OrderId);
                    }

                    [GenerateSidecar(typeof(OrderRequest), typeof(OrderResponse))]
                    private protected partial class PrivateProtectedSidecar
                    {
                        [SidecarBefore("trace")]
                        private static void Trace(SidecarContext<OrderRequest> ctx) { }
                        [SidecarHandler]
                        private static OrderResponse Handle(SidecarContext<OrderRequest> ctx) => new(ctx.Request.OrderId);
                    }

                    [GenerateSidecar(typeof(OrderRequest), typeof(OrderResponse))]
                    protected internal partial class ProtectedInternalSidecar
                    {
                        [SidecarBefore("trace")]
                        private static void Trace(SidecarContext<OrderRequest> ctx) { }
                        [SidecarHandler]
                        private static OrderResponse Handle(SidecarContext<OrderRequest> ctx) => new(ctx.Request.OrderId);
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class SidecarContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedSidecar", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedSidecar", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalSidecar", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed Sidecar type arguments")]
    [Theory]
    [InlineData("null!", "typeof(OrderResponse)")]
    [InlineData("typeof(OrderRequest)", "null!")]
    public Task Skips_Malformed_Sidecar_Type_Arguments(string requestType, string responseType)
        => Given("a Sidecar declaration with a null type argument", () => Compile($$"""
            using PatternKit.Cloud.Sidecar;
            using PatternKit.Generators.Sidecar;
            public sealed record OrderRequest(string OrderId);
            public sealed record OrderResponse(string Confirmation);
            [GenerateSidecar({{requestType}}, {{responseType}})]
            public static partial class OrderSidecars
            {
                [SidecarBefore("trace")]
                private static void Trace(SidecarContext<OrderRequest> ctx) { }
                [SidecarHandler]
                private static OrderResponse Handle(SidecarContext<OrderRequest> ctx) => new(ctx.Request.OrderId);
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
