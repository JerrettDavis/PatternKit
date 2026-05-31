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

    [Scenario("Generates health endpoint factories for abstract and sealed hosts")]
    [Fact]
    public Task Generates_Health_Endpoint_Factories_For_Abstract_And_Sealed_Hosts()
        => Given("health endpoint declarations on abstract and sealed hosts", () => Compile("""
            using PatternKit.Cloud.HealthEndpointMonitoring;
            using PatternKit.Generators.HealthEndpointMonitoring;
            namespace Demo;
            public sealed record HealthState(bool Ready);

            [GenerateHealthEndpoint(typeof(HealthState), FactoryMethodName = "CreateAbstract")]
            public abstract partial class AbstractHealthEndpoint
            {
                [HealthEndpointCheck("ready")]
                private static HealthEndpointCheckResult Ready(HealthState state) => HealthEndpointCheckResult.HealthyCheck("ready");
            }

            [GenerateHealthEndpoint(typeof(HealthState), FactoryMethodName = "CreateSealed")]
            public sealed partial class SealedHealthEndpoint
            {
                [HealthEndpointCheck("ready")]
                private static HealthEndpointCheckResult Ready(HealthState state) => HealthEndpointCheckResult.HealthyCheck("ready");
            }
            """))
        .Then("the generated source preserves host shape", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(2, result.GeneratedSources.Count);
            var generatedText = string.Join(Environment.NewLine, result.GeneratedSources);
            ScenarioExpect.Contains("public abstract partial class AbstractHealthEndpoint", generatedText);
            ScenarioExpect.Contains("CreateAbstract()", generatedText);
            ScenarioExpect.Contains("public sealed partial class SealedHealthEndpoint", generatedText);
            ScenarioExpect.Contains("CreateSealed()", generatedText);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates health endpoint source for nested accessibility variants")]
    [Fact]
    public Task Generates_Health_Endpoint_Source_For_Nested_Accessibility_Variants()
        => Given("health endpoint declarations with nested accessibility", () => CompileWithoutEmit("""
            using PatternKit.Cloud.HealthEndpointMonitoring;
            using PatternKit.Generators.HealthEndpointMonitoring;
            namespace Demo;
            public sealed record HealthState(bool Ready);

            public partial class Outer
            {
                [GenerateHealthEndpoint(typeof(HealthState), FactoryMethodName = "CreatePrivate")]
                private partial class PrivateHealthEndpoint
                {
                    [HealthEndpointCheck("private")]
                    private static HealthEndpointCheckResult Ready(HealthState state) => HealthEndpointCheckResult.HealthyCheck("private");
                }

                [GenerateHealthEndpoint(typeof(HealthState), FactoryMethodName = "CreateProtected")]
                protected partial class ProtectedHealthEndpoint
                {
                    [HealthEndpointCheck("protected")]
                    private static HealthEndpointCheckResult Ready(HealthState state) => HealthEndpointCheckResult.HealthyCheck("protected");
                }

                [GenerateHealthEndpoint(typeof(HealthState), FactoryMethodName = "CreateProtectedInternal")]
                protected internal partial class ProtectedInternalHealthEndpoint
                {
                    [HealthEndpointCheck("protected-internal")]
                    private static HealthEndpointCheckResult Ready(HealthState state) => HealthEndpointCheckResult.HealthyCheck("protected-internal");
                }

                [GenerateHealthEndpoint(typeof(HealthState), FactoryMethodName = "CreatePrivateProtected")]
                private protected partial class PrivateProtectedHealthEndpoint
                {
                    [HealthEndpointCheck("private-protected")]
                    private static HealthEndpointCheckResult Ready(HealthState state) => HealthEndpointCheckResult.HealthyCheck("private-protected");
                }
            }
            """))
        .Then("the generated source preserves accessibility", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(4, result.GeneratedSources.Count);
            var generatedText = string.Join(Environment.NewLine, result.GeneratedSources);
            ScenarioExpect.Contains("private partial class PrivateHealthEndpoint", generatedText);
            ScenarioExpect.Contains("protected partial class ProtectedHealthEndpoint", generatedText);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalHealthEndpoint", generatedText);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedHealthEndpoint", generatedText);
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

    private static GeneratorResult CompileWithoutEmit(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "HealthEndpointMonitoringGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(HealthEndpoint<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new HealthEndpointMonitoringGenerator(), out var run, out _);
        var result = run.Results.Single();
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            EmitSuccess: true,
            EmitDiagnostics: []);
    }

    private sealed record GeneratorResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        bool EmitSuccess,
        IReadOnlyList<string> EmitDiagnostics);
}
