using Microsoft.CodeAnalysis;
using PatternKit.Application.EventSourcing;
using PatternKit.Generators.EventSourcing;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Event Store generator")]
public sealed partial class EventStoreGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits event store factory")]
    [Fact]
    public Task Generator_Emits_Event_Store_Factory()
        => Given("a valid event store declaration", () => Compile("""
            using PatternKit.Generators.EventSourcing;
            namespace Demo;
            public abstract record OrderEvent(string OrderId);
            [GenerateEventStore(typeof(OrderEvent), typeof(string), FactoryName = "Build", StoreName = "order-events")]
            public static partial class OrderEventStore;
            """))
        .Then("generated source creates the store", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("InMemoryEventStore<global::Demo.OrderEvent, string>.Create(\"order-events\").Build()", source);
        })
        .AssertPassed();

    [Scenario("Generator reports invalid event store declarations")]
    [Fact]
    public Task Generator_Reports_Invalid_Event_Store_Declarations()
        => Given("a non-partial event store declaration", () => Compile("""
            using PatternKit.Generators.EventSourcing;
            public abstract record OrderEvent(string OrderId);
            [GenerateEventStore(typeof(OrderEvent), typeof(string))]
            public static class OrderEventStore;
            """))
        .Then("the partial diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PKES001"))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "EventStoreGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(InMemoryEventStore<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new EventStoreGenerator(), out var run, out _);
        var result = run.Results.Single();
        return new GeneratorResult(result.Diagnostics.ToArray(), result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources);
}
