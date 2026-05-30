using Microsoft.CodeAnalysis;
using PatternKit.Cloud.PriorityQueue;
using PatternKit.Generators.PriorityQueue;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Priority Queue generator")]
public sealed partial class PriorityQueueGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generates priority queue factory")]
    [Fact]
    public Task Generates_Priority_Queue_Factory()
        => Given("a priority queue declaration", () => Compile("""
            using PatternKit.Generators.PriorityQueue;
            namespace Demo;
            public sealed record FulfillmentWork(string OrderId, int Priority);
            [GeneratePriorityQueue(typeof(FulfillmentWork), typeof(int), FactoryMethodName = "Build", QueueName = "fulfillment-priority")]
            public static partial class FulfillmentPriorityQueue
            {
                [PriorityQueuePrioritySelector]
                private static int SelectPriority(FulfillmentWork item) => item.Priority;
            }
            """))
        .Then("the generated source creates the configured queue", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("PriorityQueuePolicy<global::Demo.FulfillmentWork, int>.Create(\"fulfillment-priority\")", source);
            ScenarioExpect.Contains(".WithPrioritySelector(SelectPriority)", source);
            ScenarioExpect.Contains(".DequeueHighestPriorityFirst()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Reports diagnostics for invalid priority queue declarations")]
    [Fact]
    public Task Reports_Diagnostics_For_Invalid_Priority_Queue_Declarations()
        => Given("invalid priority queue declarations", () => new[]
        {
            Compile("""
                using PatternKit.Generators.PriorityQueue;
                [GeneratePriorityQueue(typeof(string), typeof(int))]
                public static class PriorityQueueHost;
                """),
            Compile("""
                using PatternKit.Generators.PriorityQueue;
                [GeneratePriorityQueue(typeof(string), typeof(int))]
                public static partial class PriorityQueueHost;
                """),
            Compile("""
                using PatternKit.Generators.PriorityQueue;
                [GeneratePriorityQueue(typeof(string), typeof(int))]
                public static partial class PriorityQueueHost
                {
                    [PriorityQueuePrioritySelector]
                    private static string SelectPriority(string item) => item;
                }
                """)
        })
        .Then("diagnostics identify the invalid declarations", results =>
        {
            ScenarioExpect.Contains(results[0].Diagnostics, diagnostic => diagnostic.Id == "PKPQ001");
            ScenarioExpect.Contains(results[1].Diagnostics, diagnostic => diagnostic.Id == "PKPQ002");
            ScenarioExpect.Contains(results[2].Diagnostics, diagnostic => diagnostic.Id == "PKPQ003");
        })
        .AssertPassed();

    [Scenario("Generates lowest-first priority queues with escaped names")]
    [Fact]
    public Task Generates_Lowest_First_Priority_Queues_With_Escaped_Names()
        => Given("priority queue declaration with lowest-first ordering", () => Compile("""
            using PatternKit.Generators.PriorityQueue;
            namespace Demo;
            [GeneratePriorityQueue(typeof(string), typeof(int), QueueName = "queue\"" + "\\priority", DequeueHighestPriorityFirst = false)]
            internal partial struct PriorityDefaults
            {
                [PriorityQueuePrioritySelector]
                private static int SelectPriority(string item) => item.Length;
            }
            """))
        .Then("the generated source preserves configuration", result =>
        {
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Contains("internal partial struct PriorityDefaults", source);
            ScenarioExpect.Contains("Create(\"queue\\\"\\\\priority\")", source);
            ScenarioExpect.Contains(".DequeueLowestPriorityFirst()", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Generates priority queue inside nested hosts")]
    [Fact]
    public Task Generates_Priority_Queue_Inside_Nested_Hosts()
        => Given("a nested priority queue declaration", () => Compile("""
            using PatternKit.Generators.PriorityQueue;
            namespace Demo;
            public static partial class FulfillmentModule
            {
                internal abstract partial class Queues
                {
                    [GeneratePriorityQueue(typeof(string), typeof(int))]
                    private sealed partial class WorkPriorityQueue
                    {
                        [PriorityQueuePrioritySelector]
                        private static int SelectPriority(string item) => item.Length;
                    }
                }
            }
            """))
        .Then("the generated source recreates each containing partial type", result =>
        {
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Contains("public static partial class FulfillmentModule", source);
            ScenarioExpect.Contains("internal abstract partial class Queues", source);
            ScenarioExpect.Contains("private sealed partial class WorkPriorityQueue", source);
            ScenarioExpect.Contains(".WithPrioritySelector(SelectPriority)", source);
            ScenarioExpect.True(result.EmitSuccess, string.Join(Environment.NewLine, result.EmitDiagnostics));
        })
        .AssertPassed();

    [Scenario("Skips priority queue generation for malformed type arguments")]
    [Fact]
    public Task Skips_Priority_Queue_Generation_For_Malformed_Type_Arguments()
        => Given("priority queue declarations with unresolved item and priority types", () => new[]
        {
            Compile("""
                using PatternKit.Generators.PriorityQueue;
                [GeneratePriorityQueue(typeof(MissingItem), typeof(int))]
                public static partial class MissingItemQueue;
                """),
            Compile("""
                using PatternKit.Generators.PriorityQueue;
                [GeneratePriorityQueue(typeof(string), typeof(MissingPriority))]
                public static partial class MissingPriorityQueue;
                """)
        })
        .Then("no generated sources are produced by the generator", results =>
        {
            foreach (var result in results)
            {
                ScenarioExpect.Empty(result.Diagnostics);
                ScenarioExpect.Empty(result.GeneratedSources);
                ScenarioExpect.False(result.EmitSuccess);
            }
        })
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "PriorityQueueGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(PriorityQueuePolicy<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new PriorityQueueGenerator(), out var run, out var updated);
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
