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

    [Scenario("Generator emits store defaults and host shapes")]
    [Fact]
    public Task Generator_Emits_Store_Defaults_And_Host_Shapes()
        => Given("event store declarations using default names and different host shapes", () => Compile("""
            using PatternKit.Generators.EventSourcing;

            namespace Demo;

            public abstract record OrderEvent(string OrderId);

            [GenerateEventStore(typeof(OrderEvent), typeof(string))]
            internal abstract partial class AbstractEventStore;

            [GenerateEventStore(typeof(OrderEvent), typeof(string), StoreName = "tenant\\\"events")]
            public sealed partial class SealedEventStore;

            [GenerateEventStore(typeof(OrderEvent), typeof(System.Guid))]
            internal partial struct StructEventStore;
            """))
        .Then("generated sources preserve shape and defaults", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("internal abstract partial class AbstractEventStore", combined);
            ScenarioExpect.Contains("Create(\"AbstractEventStore\")", combined);
            ScenarioExpect.Contains("public sealed partial class SealedEventStore", combined);
            ScenarioExpect.Contains("Create(\"tenant\\\\\\\"events\")", combined);
            ScenarioExpect.Contains("internal partial struct StructEventStore", combined);
            ScenarioExpect.Contains("InMemoryEventStore<global::Demo.OrderEvent, global::System.Guid>", combined);
        })
        .AssertPassed();

    [Scenario("Generator skips malformed event store type arguments")]
    [Fact]
    public Task Generator_Skips_Malformed_Event_Store_Type_Arguments()
        => Given("an event store declaration with a null event type argument", () => Compile("""
            using PatternKit.Generators.EventSourcing;

            public abstract record OrderEvent(string OrderId);

            [GenerateEventStore(null!, typeof(string))]
            public static partial class BrokenEventStore;
            """))
        .Then("no event store source is generated", result =>
            ScenarioExpect.Empty(result.GeneratedSources))
        .AssertPassed();

    [Scenario("Generator emits nested event store hosts")]
    [Fact]
    public Task Generator_Emits_Nested_Event_Store_Hosts()
        => Given("nested event store declarations with non-public accessibility", () => CompileWithUpdated("""
            using PatternKit.Generators.EventSourcing;

            namespace Demo;

            public abstract record OrderEvent(string OrderId);

            public partial class StoreContainer
            {
                private partial class PrivateStore
                {
                    [GenerateEventStore(typeof(OrderEvent), typeof(string))]
                    protected partial class ProtectedStore;

                    [GenerateEventStore(typeof(OrderEvent), typeof(string))]
                    private protected partial class PrivateProtectedStore;

                    [GenerateEventStore(typeof(OrderEvent), typeof(string))]
                    protected internal partial class ProtectedInternalStore;
                }
            }
            """))
        .Then("generated sources preserve containing type wrappers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            ScenarioExpect.Equal(3, result.GeneratedSources.Count);

            var combined = string.Join("\n", result.GeneratedSources);
            ScenarioExpect.Contains("public partial class StoreContainer", combined);
            ScenarioExpect.Contains("private partial class PrivateStore", combined);
            ScenarioExpect.Contains("protected partial class ProtectedStore", combined);
            ScenarioExpect.Contains("private protected partial class PrivateProtectedStore", combined);
            ScenarioExpect.Contains("protected internal partial class ProtectedInternalStore", combined);

            var emit = result.Updated.Emit(Stream.Null);
            ScenarioExpect.True(emit.Success, string.Join("\n", emit.Diagnostics));
        })
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

    private static GeneratorCompilationResult CompileWithUpdated(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "EventStoreGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(InMemoryEventStore<,>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new EventStoreGenerator(), out var run, out var updated);
        var result = run.Results.Single();
        return new GeneratorCompilationResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray(),
            updated);
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources);

    private sealed record GeneratorCompilationResult(
        IReadOnlyList<Diagnostic> Diagnostics,
        IReadOnlyList<string> GeneratedSources,
        Compilation Updated);
}
