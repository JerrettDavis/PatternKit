using Microsoft.CodeAnalysis;
using PatternKit.Cloud.BackendsForFrontends;
using PatternKit.Generators.BackendsForFrontends;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Backends for Frontends generator")]
public sealed partial class BackendsForFrontendsGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates Backends for Frontends factory")]
    [Fact]
    public Task Generates_Backends_For_Frontends_Factory()
        => Given("a BFF declaration", () => Compile("""
            using PatternKit.Cloud.BackendsForFrontends;
            using PatternKit.Generators.BackendsForFrontends;
            namespace Demo;
            public sealed record ClientRequest(string Client, string CustomerId);
            public sealed record ClientResponse(string CustomerId, string Shape);
            [GenerateBackendsForFrontends(typeof(ClientRequest), typeof(ClientResponse), FactoryMethodName = "Build", GatewayName = "commerce-bff")]
            public static partial class CommerceBff
            {
                [FrontendSelector("mobile")]
                private static bool IsMobile(ClientRequest request) => request.Client == "mobile";
                [FrontendHandler("mobile")]
                private static ClientResponse Mobile(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "compact");
                [FrontendFallback]
                private static ClientResponse Fallback(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "standard");
            }
            """))
        .Then("the generated source creates the configured BFF", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("BackendsForFrontends<global::Demo.ClientRequest, global::Demo.ClientResponse>.Create(\"commerce-bff\")", source);
            ScenarioExpect.Contains(".Frontend(\"mobile\", IsMobile, Mobile)", source);
            ScenarioExpect.Contains(".Fallback(Fallback)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid Backends for Frontends declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Backends_For_Frontends_Declarations()
        => Given("invalid BFF declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.BackendsForFrontends;
                [GenerateBackendsForFrontends(typeof(string), typeof(int))]
                public static class BffHost;
                """),
            Compile("""
                using PatternKit.Generators.BackendsForFrontends;
                [GenerateBackendsForFrontends(typeof(string), typeof(int))]
                public static partial class BffHost;
                """),
            Compile("""
                using PatternKit.Cloud.BackendsForFrontends;
                using PatternKit.Generators.BackendsForFrontends;
                [GenerateBackendsForFrontends(typeof(string), typeof(int))]
                public static partial class BffHost
                {
                    [FrontendSelector("web")]
                    private static string Web(string value) => value;
                    [FrontendHandler("web")]
                    private static int Handle(BackendsForFrontendsContext<string> ctx) => 1;
                }
                """),
            Compile("""
                using PatternKit.Cloud.BackendsForFrontends;
                using PatternKit.Generators.BackendsForFrontends;
                [GenerateBackendsForFrontends(typeof(string), typeof(int))]
                public static partial class BffHost
                {
                    [FrontendSelector("web")]
                    private static bool Web(string value) => true;
                    [FrontendSelector("WEB")]
                    private static bool Web2(string value) => true;
                    [FrontendHandler("web")]
                    private static int Handle(BackendsForFrontendsContext<string> ctx) => 1;
                    [FrontendHandler("WEB")]
                    private static int Handle2(BackendsForFrontendsContext<string> ctx) => 1;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKBFF001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKBFF002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKBFF003");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKBFF004");
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "BackendsForFrontendsGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(BackendsForFrontends<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new BackendsForFrontendsGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
