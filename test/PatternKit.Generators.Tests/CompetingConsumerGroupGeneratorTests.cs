using Microsoft.CodeAnalysis;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.CompetingConsumers;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Competing Consumers generator")]
public sealed partial class CompetingConsumerGroupGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates competing consumer group builder factory")]
    [Fact]
    public Task Generates_Competing_Consumer_Group_Builder_Factory()
        => Given("a competing consumer group declaration", () => Compile("""
            using PatternKit.Generators.Messaging;

            namespace Demo;

            public sealed record FulfillmentWork(string OrderId);
            public sealed record FulfillmentResult(string OrderId, string Consumer);

            [GenerateCompetingConsumerGroup(typeof(FulfillmentWork), typeof(FulfillmentResult), FactoryMethodName = "Build", GroupName = "fulfillment-consumers", MaxConcurrentDeliveries = 4)]
            public static partial class FulfillmentConsumers;
            """))
        .Then("the generated source creates the configured builder", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("CompetingConsumerGroup<global::Demo.FulfillmentWork, global::Demo.FulfillmentResult>.Create(\"fulfillment-consumers\")", source);
            ScenarioExpect.Contains(".WithMaxConcurrentDeliveries(4)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid competing consumer declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Competing_Consumer_Declarations()
        => Given("invalid competing consumer declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.Messaging;
                [GenerateCompetingConsumerGroup(typeof(string), typeof(int))]
                public static class ConsumerHost;
                """),
            Compile("""
                using PatternKit.Generators.Messaging;
                [GenerateCompetingConsumerGroup(typeof(string), typeof(int), MaxConcurrentDeliveries = 0)]
                public static partial class ConsumerHost;
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKCNS001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKCNS002");
        })
        .AssertPassed();

    [Scenario("Generates competing consumer group defaults and type shapes")]
    [Fact]
    public Task Generates_Competing_Consumer_Group_Defaults_And_Type_Shapes()
        => Given("competing consumer declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.Messaging;

            namespace Demo;

            public sealed record FulfillmentWork(string OrderId);
            public sealed record FulfillmentResult(string OrderId, string Consumer);

            [GenerateCompetingConsumerGroup(typeof(FulfillmentWork), typeof(FulfillmentResult))]
            internal abstract partial class AbstractConsumers;

            [GenerateCompetingConsumerGroup(typeof(FulfillmentWork), typeof(FulfillmentResult), GroupName = "tenant\\\"consumers", MaxConcurrentDeliveries = 3)]
            public sealed partial class SealedConsumers;

            [GenerateCompetingConsumerGroup(typeof(FulfillmentWork), typeof(FulfillmentResult))]
            internal partial struct StructConsumers;
            """))
        .Then("generated sources preserve host shape and configured names", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractConsumers", combined);
            ScenarioExpect.Contains("Create()", combined);
            ScenarioExpect.Contains("Create(\"competing-consumers\")", combined);
            ScenarioExpect.Contains(".WithMaxConcurrentDeliveries(1)", combined);
            ScenarioExpect.Contains("public sealed partial class SealedConsumers", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"consumers\")", combined);
            ScenarioExpect.Contains(".WithMaxConcurrentDeliveries(3)", combined);
            ScenarioExpect.Contains("internal partial struct StructConsumers", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates nested competing consumer group host wrappers")]
    [Fact]
    public Task Generates_Nested_Competing_Consumer_Group_Host_Wrappers()
        => Given("nested competing consumer declarations with non-public accessibility", () => Compile("""
            using PatternKit.Generators.Messaging;

            namespace Demo;

            public sealed record FulfillmentWork(string OrderId);
            public sealed record FulfillmentResult(string OrderId, string Consumer);

            public partial class ConsumerContainer
            {
                private partial class PrivateHost
                {
                    [GenerateCompetingConsumerGroup(typeof(FulfillmentWork), typeof(FulfillmentResult))]
                    protected partial class ProtectedConsumers;

                    [GenerateCompetingConsumerGroup(typeof(FulfillmentWork), typeof(FulfillmentResult))]
                    private protected partial class PrivateProtectedConsumers;

                    [GenerateCompetingConsumerGroup(typeof(FulfillmentWork), typeof(FulfillmentResult))]
                    protected internal partial class ProtectedInternalConsumers;
                }
            }
            """))
        .Then("generated sources preserve containing partial type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class ConsumerContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateHost", combined);
            ScenarioExpect.Contains("protected partial class ProtectedConsumers", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedConsumers", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalConsumers", combined);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips malformed competing consumer group type arguments")]
    [Theory]
    [InlineData("null!", "typeof(FulfillmentResult)")]
    [InlineData("typeof(FulfillmentWork)", "null!")]
    public Task Skips_Malformed_Competing_Consumer_Group_Type_Arguments(string messageType, string resultType)
        => Given("a competing consumer declaration with a null type argument", () => Compile($$"""
            using PatternKit.Generators.Messaging;

            public sealed record FulfillmentWork(string OrderId);
            public sealed record FulfillmentResult(string OrderId, string Consumer);

            [GenerateCompetingConsumerGroup({{messageType}}, {{resultType}})]
            public static partial class FulfillmentConsumers;
            """))
        .Then("no source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "CompetingConsumerGroupGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(CompetingConsumerGroup<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new CompetingConsumerGroupGenerator(), out var run, out var updated);
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
