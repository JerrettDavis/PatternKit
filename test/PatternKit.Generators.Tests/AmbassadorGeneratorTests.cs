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
    [Theory]
    [InlineData("public static class AmbassadorHost { [AmbassadorCall] private static int Call(AmbassadorContext<string> ctx) => 1; }", "PKAMB001")]
    [InlineData("public static partial class AmbassadorHost;", "PKAMB002")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorCall] private static int One(AmbassadorContext<string> ctx) => 1; [AmbassadorCall] private static int Two(AmbassadorContext<string> ctx) => 2; }", "PKAMB002")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorConnectionPolicy] private static bool One(string request) => true; [AmbassadorConnectionPolicy] private static bool Two(string request) => true; [AmbassadorCall] private static int Call(AmbassadorContext<string> ctx) => 1; }", "PKAMB002")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorFallback] private static int One(AmbassadorContext<string> ctx) => 1; [AmbassadorFallback] private static int Two(AmbassadorContext<string> ctx) => 2; [AmbassadorCall] private static int Call(AmbassadorContext<string> ctx) => 1; }", "PKAMB002")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorTransform] private static int Transform(string request) => 1; [AmbassadorCall] private static int Call(AmbassadorContext<string> ctx) => 1; }", "PKAMB003")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorConnectionPolicy] private static string CanConnect(string request) => request; [AmbassadorCall] private static int Call(AmbassadorContext<string> ctx) => 1; }", "PKAMB003")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorTelemetry(\"trace\")] private static int Trace(AmbassadorContext<string> ctx) => 1; [AmbassadorCall] private static int Call(AmbassadorContext<string> ctx) => 1; }", "PKAMB003")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorCall] private static string Call(AmbassadorContext<string> ctx) => \"\"; }", "PKAMB003")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorCall] private static int Call(AmbassadorContext<string> ctx) => 1; [AmbassadorFallback] private static string Fallback(AmbassadorContext<string> ctx) => \"\"; }", "PKAMB003")]
    [InlineData("public static partial class AmbassadorHost { [AmbassadorTelemetry(\"trace\")] private static void Trace(AmbassadorContext<string> ctx) { } [AmbassadorTelemetry(\"TRACE\")] private static void Trace2(AmbassadorContext<string> ctx) { } [AmbassadorCall] private static int Call(AmbassadorContext<string> ctx) => 1; }", "PKAMB004")]
    public Task Reports_Diagnostics_For_Invalid_Ambassador_Declarations(string declaration, string expected)
        => Given("an invalid ambassador declaration", () => Compile($$"""
            using PatternKit.Cloud.Ambassador;
            using PatternKit.Generators.Ambassador;
            [GenerateAmbassador(typeof(string), typeof(int))]
            {{declaration}}
            """))
        .Then("diagnostics identify invalid declarations", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expected))
        .AssertPassed();

    [Scenario("Generates ambassador defaults and host shapes")]
    [Fact]
    public Task Generates_Ambassador_Defaults_And_Host_Shapes()
        => Given("ambassador declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Cloud.Ambassador;
            using PatternKit.Generators.Ambassador;
            namespace Demo;
            public sealed record InventoryRequest(string Sku);
            public sealed record InventoryResponse(string Status);

            [GenerateAmbassador(typeof(InventoryRequest), typeof(InventoryResponse))]
            internal abstract partial class AbstractAmbassador
            {
                [AmbassadorCall]
                private static InventoryResponse Call(AmbassadorContext<InventoryRequest> ctx) => new("ok");
            }

            [GenerateAmbassador(typeof(InventoryRequest), typeof(InventoryResponse), AmbassadorName = "tenant\\\"ambassador")]
            public sealed partial class SealedAmbassador
            {
                [AmbassadorCall]
                private static InventoryResponse Call(AmbassadorContext<InventoryRequest> ctx) => new("ok");
            }

            [GenerateAmbassador(typeof(InventoryRequest), typeof(InventoryResponse))]
            internal partial struct StructAmbassador
            {
                [AmbassadorCall]
                private static InventoryResponse Call(AmbassadorContext<InventoryRequest> ctx) => new("ok");
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractAmbassador", combined);
            ScenarioExpect.Contains("public sealed partial class SealedAmbassador", combined);
            ScenarioExpect.Contains("internal partial struct StructAmbassador", combined);
            ScenarioExpect.Contains("Create(\"ambassador\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"ambassador\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested ambassador host wrappers")]
    [Fact]
    public Task Generates_Nested_Ambassador_Host_Wrappers()
        => Given("nested ambassador declarations", () => Compile("""
            using PatternKit.Cloud.Ambassador;
            using PatternKit.Generators.Ambassador;
            namespace Demo;
            public sealed record InventoryRequest(string Sku);
            public sealed record InventoryResponse(string Status);

            public partial class AmbassadorContainer
            {
                private partial class PrivateHost
                {
                    [GenerateAmbassador(typeof(InventoryRequest), typeof(InventoryResponse))]
                    protected partial class ProtectedAmbassador
                    {
                        [AmbassadorCall]
                        private static InventoryResponse Call(AmbassadorContext<InventoryRequest> ctx) => new("ok");
                    }

                    [GenerateAmbassador(typeof(InventoryRequest), typeof(InventoryResponse))]
                    private protected partial class PrivateProtectedAmbassador
                    {
                        [AmbassadorCall]
                        private static InventoryResponse Call(AmbassadorContext<InventoryRequest> ctx) => new("ok");
                    }

                    [GenerateAmbassador(typeof(InventoryRequest), typeof(InventoryResponse))]
                    protected internal partial class ProtectedInternalAmbassador
                    {
                        [AmbassadorCall]
                        private static InventoryResponse Call(AmbassadorContext<InventoryRequest> ctx) => new("ok");
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class AmbassadorContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedAmbassador", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedAmbassador", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalAmbassador", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed ambassador type arguments")]
    [Theory]
    [InlineData("null!", "typeof(InventoryResponse)")]
    [InlineData("typeof(InventoryRequest)", "null!")]
    public Task Skips_Malformed_Ambassador_Type_Arguments(string requestType, string responseType)
        => Given("an ambassador declaration with a null type argument", () => Compile($$"""
            using PatternKit.Cloud.Ambassador;
            using PatternKit.Generators.Ambassador;
            public sealed record InventoryRequest(string Sku);
            public sealed record InventoryResponse(string Status);
            [GenerateAmbassador({{requestType}}, {{responseType}})]
            public static partial class InventoryAmbassador
            {
                [AmbassadorCall]
                private static InventoryResponse Call(AmbassadorContext<InventoryRequest> ctx) => new("ok");
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
