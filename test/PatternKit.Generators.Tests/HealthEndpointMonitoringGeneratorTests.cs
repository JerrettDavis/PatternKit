using Microsoft.CodeAnalysis;
using PatternKit.Cloud.HealthEndpointMonitoring;
using PatternKit.Generators.HealthEndpointMonitoring;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Health Endpoint Monitoring generator")]
public sealed partial class HealthEndpointMonitoringGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates health endpoint factory")]
    [Fact]
    public Task Generates_Health_Endpoint_Factory()
        => Given("a health endpoint declaration", () => Compile("""
            using PatternKit.Cloud.HealthEndpointMonitoring;
            using PatternKit.Generators.HealthEndpointMonitoring;
            namespace Demo;
            public sealed record FulfillmentHealth(bool DatabaseOnline, int QueueDepth);
            [GenerateHealthEndpoint(typeof(FulfillmentHealth), FactoryMethodName = "Build", EndpointName = "fulfillment-health")]
            public static partial class FulfillmentHealthEndpoint
            {
                [HealthEndpointCheck("database", Order = 2)]
                private static HealthEndpointCheckResult Database(FulfillmentHealth health) => HealthEndpointCheckResult.HealthyCheck("database");

                [HealthEndpointCheck("queue-depth", Order = 1)]
                private static HealthEndpointCheckResult Queue(FulfillmentHealth health) => HealthEndpointCheckResult.HealthyCheck("queue-depth");
            }
            """))
        .Then("the generated source creates the configured endpoint", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("HealthEndpoint<global::Demo.FulfillmentHealth>.Create(\"fulfillment-health\")", source);
            ScenarioExpect.True(source.IndexOf(".WithCheck(\"queue-depth\", Queue)", StringComparison.Ordinal) < source.IndexOf(".WithCheck(\"database\", Database)", StringComparison.Ordinal));
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid health endpoint declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Health_Endpoint_Declarations()
        => Given("invalid health endpoint declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.HealthEndpointMonitoring;
                [GenerateHealthEndpoint(typeof(string))]
                public static class HealthEndpointHost;
                """),
            Compile("""
                using PatternKit.Generators.HealthEndpointMonitoring;
                [GenerateHealthEndpoint(typeof(string))]
                public static partial class HealthEndpointHost;
                """),
            Compile("""
                using PatternKit.Generators.HealthEndpointMonitoring;
                [GenerateHealthEndpoint(typeof(string))]
                public static partial class HealthEndpointHost
                {
                    [HealthEndpointCheck]
                    private static string Check(string value) => value;
                }
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKHEM001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKHEM002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKHEM003");
        })
        .AssertPassed();

    [Scenario("Generates default check names with escaped endpoint names")]
    [Fact]
    public Task Generates_Default_Check_Names_With_Escaped_Endpoint_Names()
        => Given("a health endpoint declaration with default check name", () => Compile("""
            using PatternKit.Cloud.HealthEndpointMonitoring;
            using PatternKit.Generators.HealthEndpointMonitoring;
            namespace Demo;
            [GenerateHealthEndpoint(typeof(string), EndpointName = "health\"" + "\\endpoint")]
            internal partial struct HealthDefaults
            {
                [HealthEndpointCheck]
                private static HealthEndpointCheckResult Value(string value) => HealthEndpointCheckResult.HealthyCheck("value");
            }
            """))
        .Then("the generated source preserves configuration", result =>
        {
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Contains("internal partial struct HealthDefaults", source);
            ScenarioExpect.Contains("Create(\"health\\\"\\\\endpoint\")", source);
            ScenarioExpect.Contains(".WithCheck(\"Value\", Value)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "HealthEndpointMonitoringGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(HealthEndpoint<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new HealthEndpointMonitoringGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        var emit = updated.Emit(Stream.Null);
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            emit.Success,
            emit.Diagnostics.Select(static diagnostic => diagnostic.ToString()).ToArray());
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
