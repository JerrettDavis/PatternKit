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
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Gateway_Aggregation_Declarations()
        => Given("invalid gateway aggregation declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.GatewayAggregation;
                [GenerateGatewayAggregation(typeof(string), typeof(int))]
                public static class GatewayHost;
                """),
            Compile("""
                using PatternKit.Generators.GatewayAggregation;
                [GenerateGatewayAggregation(typeof(string), typeof(int))]
                public static partial class GatewayHost;
                """),
            Compile("""
                using PatternKit.Cloud.GatewayAggregation;
                using PatternKit.Generators.GatewayAggregation;
                [GenerateGatewayAggregation(typeof(string), typeof(int))]
                public static partial class GatewayHost
                {
                    [GatewayAggregationFetch("profile")]
                    private static void Profile(string value) { }
                    [GatewayAggregationComposer]
                    private static int Compose(GatewayAggregationContext<string> ctx) => 1;
                }
                """),
            Compile("""
                using PatternKit.Cloud.GatewayAggregation;
                using PatternKit.Generators.GatewayAggregation;
                [GenerateGatewayAggregation(typeof(string), typeof(int))]
                public static partial class GatewayHost
                {
                    [GatewayAggregationFetch("profile")]
                    private static string Profile(string value) => value;
                    [GatewayAggregationFetch("PROFILE")]
                    private static string Profile2(string value) => value;
                    [GatewayAggregationComposer]
                    private static int Compose(GatewayAggregationContext<string> ctx) => 1;
                }
                """)
        })
        .Then("diagnostics identify invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKGA001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKGA002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKGA003");
            ScenarioExpect.Contains(results[3].Diagnostics, diagnostic => diagnostic.Id == "PKGA004");
        })
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
