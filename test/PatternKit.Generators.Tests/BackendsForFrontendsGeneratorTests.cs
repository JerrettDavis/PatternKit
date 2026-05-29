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
    [Theory]
    [InlineData("public static class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; }", "PKBFF001")]
    [InlineData("public static partial class BffHost;", "PKBFF002")]
    [InlineData("public static partial class BffHost { [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; }", "PKBFF002")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; }", "PKBFF002")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; [FrontendHandler(\"mobile\")] private static int Mobile(BackendsForFrontendsContext<string> ctx) => 1; }", "PKBFF002")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; [FrontendFallback] private static int One(BackendsForFrontendsContext<string> ctx) => 1; [FrontendFallback] private static int Two(BackendsForFrontendsContext<string> ctx) => 2; }", "PKBFF002")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool One(string value) => true; [FrontendSelector(\"WEB\")] private static bool Two(string value) => true; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; [FrontendHandler(\"WEB\")] private static int Web2(BackendsForFrontendsContext<string> ctx) => 2; }", "PKBFF004")]
    [InlineData("public partial class BffHost { [FrontendSelector(\"web\")] private bool IsWeb(string value) => true; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; }", "PKBFF003")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static string IsWeb(string value) => value; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; }", "PKBFF003")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb() => true; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; }", "PKBFF003")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(int value) => true; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; }", "PKBFF003")]
    [InlineData("public partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; [FrontendHandler(\"web\")] private int Web(BackendsForFrontendsContext<string> ctx) => 1; }", "PKBFF003")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; [FrontendHandler(\"web\")] private static string Web(BackendsForFrontendsContext<string> ctx) => string.Empty; }", "PKBFF003")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; [FrontendHandler(\"web\")] private static int Web(string ctx) => 1; }", "PKBFF003")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<int> ctx) => 1; }", "PKBFF003")]
    [InlineData("public static partial class BffHost { [FrontendSelector(\"web\")] private static bool IsWeb(string value) => true; [FrontendHandler(\"web\")] private static int Web(BackendsForFrontendsContext<string> ctx) => 1; [FrontendFallback] private static string Fallback(BackendsForFrontendsContext<string> ctx) => string.Empty; }", "PKBFF003")]
    public Task Reports_Diagnostics_For_Invalid_Backends_For_Frontends_Declarations(string declaration, string diagnosticId)
        => Given("an invalid BFF declaration", () => Compile($$"""
            using PatternKit.Cloud.BackendsForFrontends;
            using PatternKit.Generators.BackendsForFrontends;
            [GenerateBackendsForFrontends(typeof(string), typeof(int))]
            {{declaration}}
            """))
        .Then("diagnostics identify invalid declarations", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates Backends for Frontends defaults and host shapes")]
    [Fact]
    public Task Generates_Backends_For_Frontends_Defaults_And_Host_Shapes()
        => Given("BFF declarations with default names and different host shapes", () => Compile("""
            using PatternKit.Cloud.BackendsForFrontends;
            using PatternKit.Generators.BackendsForFrontends;
            namespace Demo;
            public sealed record ClientRequest(string Client, string CustomerId);
            public sealed record ClientResponse(string CustomerId, string Shape);

            [GenerateBackendsForFrontends(typeof(ClientRequest), typeof(ClientResponse))]
            internal abstract partial class AbstractBff
            {
                [FrontendSelector("web")]
                private static bool IsWeb(ClientRequest request) => true;
                [FrontendHandler("web")]
                private static ClientResponse Web(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "web");
            }

            [GenerateBackendsForFrontends(typeof(ClientRequest), typeof(ClientResponse), GatewayName = "tenant\\\"bff")]
            public sealed partial class SealedBff
            {
                [FrontendSelector("web")]
                private static bool IsWeb(ClientRequest request) => true;
                [FrontendHandler("web")]
                private static ClientResponse Web(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "web");
            }

            [GenerateBackendsForFrontends(typeof(ClientRequest), typeof(ClientResponse))]
            internal partial struct StructBff
            {
                [FrontendSelector("web")]
                private static bool IsWeb(ClientRequest request) => true;
                [FrontendHandler("web")]
                private static ClientResponse Web(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "web");
            }
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractBff", combined);
            ScenarioExpect.Contains("public sealed partial class SealedBff", combined);
            ScenarioExpect.Contains("internal partial struct StructBff", combined);
            ScenarioExpect.Contains("Create(\"backends-for-frontends\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"bff\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested Backends for Frontends host wrappers")]
    [Fact]
    public Task Generates_Nested_Backends_For_Frontends_Host_Wrappers()
        => Given("nested BFF declarations", () => Compile("""
            using PatternKit.Cloud.BackendsForFrontends;
            using PatternKit.Generators.BackendsForFrontends;
            namespace Demo;
            public sealed record ClientRequest(string Client, string CustomerId);
            public sealed record ClientResponse(string CustomerId, string Shape);

            public partial class BffContainer
            {
                private partial class PrivateHost
                {
                    [GenerateBackendsForFrontends(typeof(ClientRequest), typeof(ClientResponse))]
                    protected partial class ProtectedBff
                    {
                        [FrontendSelector("web")]
                        private static bool IsWeb(ClientRequest request) => true;
                        [FrontendHandler("web")]
                        private static ClientResponse Web(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "web");
                    }

                    [GenerateBackendsForFrontends(typeof(ClientRequest), typeof(ClientResponse))]
                    private protected partial class PrivateProtectedBff
                    {
                        [FrontendSelector("web")]
                        private static bool IsWeb(ClientRequest request) => true;
                        [FrontendHandler("web")]
                        private static ClientResponse Web(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "web");
                    }

                    [GenerateBackendsForFrontends(typeof(ClientRequest), typeof(ClientResponse))]
                    protected internal partial class ProtectedInternalBff
                    {
                        [FrontendSelector("web")]
                        private static bool IsWeb(ClientRequest request) => true;
                        [FrontendHandler("web")]
                        private static ClientResponse Web(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "web");
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class BffContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedBff", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedBff", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalBff", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed Backends for Frontends type arguments")]
    [Theory]
    [InlineData("null!", "typeof(ClientResponse)")]
    [InlineData("typeof(ClientRequest)", "null!")]
    public Task Skips_Malformed_Backends_For_Frontends_Type_Arguments(string requestType, string responseType)
        => Given("a BFF declaration with a null type argument", () => Compile($$"""
            using PatternKit.Cloud.BackendsForFrontends;
            using PatternKit.Generators.BackendsForFrontends;
            public sealed record ClientRequest(string Client, string CustomerId);
            public sealed record ClientResponse(string CustomerId, string Shape);
            [GenerateBackendsForFrontends({{requestType}}, {{responseType}})]
            public static partial class CommerceBff
            {
                [FrontendSelector("web")]
                private static bool IsWeb(ClientRequest request) => true;
                [FrontendHandler("web")]
                private static ClientResponse Web(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "web");
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
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
