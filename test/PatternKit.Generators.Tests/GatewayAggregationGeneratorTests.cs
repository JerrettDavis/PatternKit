using Microsoft.CodeAnalysis;
using PatternKit.Cloud.GatewayAggregation;
using PatternKit.Generators.GatewayAggregation;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Gateway Aggregation generator")]
public sealed partial class GatewayAggregationGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates gateway aggregation factory")]
    [Fact]
    public Task Generates_Gateway_Aggregation_Factory()
        => Given("a gateway aggregation declaration", () => Compile("""
            using PatternKit.Cloud.GatewayAggregation;
            using PatternKit.Generators.GatewayAggregation;
            namespace Demo;
            public sealed record DashboardRequest(string CustomerId);
            public sealed record CustomerProfile(string CustomerId, string Name);
            public sealed record OrderSummary(string CustomerId, int OpenOrders);
            public sealed record DashboardResponse(string CustomerId, int OpenOrders);
            [GenerateGatewayAggregation(typeof(DashboardRequest), typeof(DashboardResponse), FactoryMethodName = "Build", GatewayName = "customer-dashboard")]
            public static partial class CustomerDashboardGateway
            {
                [GatewayAggregationFetch("profile")]
                private static CustomerProfile Profile(DashboardRequest request) => new(request.CustomerId, "Ada");
                [GatewayAggregationFetch("orders")]
                private static OrderSummary Orders(DashboardRequest request) => new(request.CustomerId, 2);
                [GatewayAggregationComposer]
                private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Require<CustomerProfile>("profile").CustomerId, ctx.Require<OrderSummary>("orders").OpenOrders);
            }
            """))
        .Then("the generated source creates the configured gateway", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("GatewayAggregation<global::Demo.DashboardRequest, global::Demo.DashboardResponse>.Create(\"customer-dashboard\")", source);
            ScenarioExpect.Contains(".Fetch<global::Demo.CustomerProfile>(\"profile\", Profile)", source);
            ScenarioExpect.Contains(".Fetch<global::Demo.OrderSummary>(\"orders\", Orders)", source);
            ScenarioExpect.Contains(".Compose(Compose)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid gateway aggregation declarations")]
    [Theory]
    [InlineData("public static class GatewayHost;", "PKGA001")]
    [InlineData("public static partial class GatewayHost;", "PKGA002")]
    [InlineData("public static partial class GatewayHost { [GatewayAggregationFetch(\"profile\")] private static void Profile(DashboardRequest request) { } [GatewayAggregationComposer] private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId); }", "PKGA003")]
    [InlineData("public static partial class GatewayHost { [GatewayAggregationFetch(\"profile\")] private DashboardRequest Profile(DashboardRequest request) => request; [GatewayAggregationComposer] private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId); }", "PKGA003")]
    [InlineData("public static partial class GatewayHost { [GatewayAggregationFetch(\"profile\")] private static DashboardRequest Profile(string request) => new(request); [GatewayAggregationComposer] private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId); }", "PKGA003")]
    [InlineData("public static partial class GatewayHost { [GatewayAggregationFetch(\"profile\")] private static DashboardRequest Profile(DashboardRequest request) => request; [GatewayAggregationComposer] private static string Compose(GatewayAggregationContext<DashboardRequest> ctx) => ctx.Request.CustomerId; }", "PKGA003")]
    [InlineData("public static partial class GatewayHost { [GatewayAggregationFetch(\"profile\")] private static DashboardRequest Profile(DashboardRequest request) => request; [GatewayAggregationComposer] private DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId); }", "PKGA003")]
    [InlineData("public static partial class GatewayHost { [GatewayAggregationFetch(\"profile\")] private static DashboardRequest Profile(DashboardRequest request) => request; [GatewayAggregationComposer] private static DashboardResponse Compose(string ctx) => new(ctx); }", "PKGA003")]
    [InlineData("public static partial class GatewayHost { [GatewayAggregationFetch(\"profile\")] private static DashboardRequest Profile(DashboardRequest request) => request; [GatewayAggregationFetch(\"PROFILE\")] private static DashboardRequest Profile2(DashboardRequest request) => request; [GatewayAggregationComposer] private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId); }", "PKGA004")]
    public Task Reports_Diagnostics_For_Invalid_Gateway_Aggregation_Declarations(string declaration, string diagnosticId)
        => Given("an invalid gateway aggregation declaration", () => Compile($$"""
            using PatternKit.Cloud.GatewayAggregation;
            using PatternKit.Generators.GatewayAggregation;
            public sealed record DashboardRequest(string CustomerId);
            public sealed record DashboardResponse(string CustomerId);
            [GenerateGatewayAggregation(typeof(DashboardRequest), typeof(DashboardResponse))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    [Scenario("Generates gateway aggregation defaults and host shapes")]
    [Fact]
    public Task Generates_Gateway_Aggregation_Defaults_And_Host_Shapes()
        => Given("gateway aggregation declarations with default names and host shapes", () => Compile("""
            using PatternKit.Cloud.GatewayAggregation;
            using PatternKit.Generators.GatewayAggregation;
            namespace Demo;
            public sealed record DashboardRequest(string CustomerId);
            public sealed record DashboardResponse(string CustomerId);

            [GenerateGatewayAggregation(typeof(DashboardRequest), typeof(DashboardResponse))]
            internal abstract partial class AbstractGateway
            {
                [GatewayAggregationFetch("profile")]
                private static DashboardRequest Profile(DashboardRequest request) => request;
                [GatewayAggregationComposer]
                private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId);
            }

            [GenerateGatewayAggregation(typeof(DashboardRequest), typeof(DashboardResponse), GatewayName = "tenant\\\"gateway")]
            public sealed partial class SealedGateway
            {
                [GatewayAggregationFetch("profile")]
                private static DashboardRequest Profile(DashboardRequest request) => request;
                [GatewayAggregationComposer]
                private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId);
            }

            [GenerateGatewayAggregation(typeof(DashboardRequest), typeof(DashboardResponse))]
            internal partial struct StructGateway
            {
                [GatewayAggregationFetch("profile")]
                private static DashboardRequest Profile(DashboardRequest request) => request;
                [GatewayAggregationComposer]
                private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId);
            }
            """))
        .Then("generated sources preserve host shape and configured defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractGateway", combined);
            ScenarioExpect.Contains("public sealed partial class SealedGateway", combined);
            ScenarioExpect.Contains("internal partial struct StructGateway", combined);
            ScenarioExpect.Contains("Create(\"gateway-aggregation\")", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"gateway\")", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested gateway aggregation host wrappers")]
    [Fact]
    public Task Generates_Nested_Gateway_Aggregation_Host_Wrappers()
        => Given("nested gateway aggregation declarations", () => Compile("""
            using PatternKit.Cloud.GatewayAggregation;
            using PatternKit.Generators.GatewayAggregation;
            namespace Demo;
            public sealed record DashboardRequest(string CustomerId);
            public sealed record DashboardResponse(string CustomerId);

            public partial class GatewayContainer
            {
                private partial class PrivateHost
                {
                    [GenerateGatewayAggregation(typeof(DashboardRequest), typeof(DashboardResponse))]
                    protected partial class ProtectedGateway
                    {
                        [GatewayAggregationFetch("profile")]
                        private static DashboardRequest Profile(DashboardRequest request) => request;
                        [GatewayAggregationComposer]
                        private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId);
                    }

                    [GenerateGatewayAggregation(typeof(DashboardRequest), typeof(DashboardResponse))]
                    private protected partial class PrivateProtectedGateway
                    {
                        [GatewayAggregationFetch("profile")]
                        private static DashboardRequest Profile(DashboardRequest request) => request;
                        [GatewayAggregationComposer]
                        private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId);
                    }

                    [GenerateGatewayAggregation(typeof(DashboardRequest), typeof(DashboardResponse))]
                    protected internal partial class ProtectedInternalGateway
                    {
                        [GatewayAggregationFetch("profile")]
                        private static DashboardRequest Profile(DashboardRequest request) => request;
                        [GatewayAggregationComposer]
                        private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId);
                    }
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class GatewayContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedGateway", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedGateway", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalGateway", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed gateway aggregation type arguments")]
    [Theory]
    [InlineData("null!", "typeof(DashboardResponse)")]
    [InlineData("typeof(DashboardRequest)", "null!")]
    public Task Skips_Malformed_Gateway_Aggregation_Type_Arguments(string requestType, string responseType)
        => Given("a gateway aggregation declaration with a null type argument", () => Compile($$"""
            using PatternKit.Cloud.GatewayAggregation;
            using PatternKit.Generators.GatewayAggregation;
            public sealed record DashboardRequest(string CustomerId);
            public sealed record DashboardResponse(string CustomerId);
            [GenerateGatewayAggregation({{requestType}}, {{responseType}})]
            public static partial class GatewayHost
            {
                [GatewayAggregationFetch("profile")]
                private static DashboardRequest Profile(DashboardRequest request) => request;
                [GatewayAggregationComposer]
                private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx) => new(ctx.Request.CustomerId);
            }
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "GatewayAggregationGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(GatewayAggregation<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new GatewayAggregationGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(), emit.Success, emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources, bool EmitSuccess, IReadOnlyList<string> EmitDiagnostics);
}
