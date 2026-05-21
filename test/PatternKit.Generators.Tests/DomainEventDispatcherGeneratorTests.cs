using Microsoft.CodeAnalysis;
using PatternKit.Application.DomainEvents;
using PatternKit.Generators.DomainEvents;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Generators.Tests;

[Feature("Domain Event dispatcher generator")]
public sealed partial class DomainEventDispatcherGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Generator emits domain event dispatcher factory")]
    [Fact]
    public Task Generator_Emits_Domain_Event_Dispatcher_Factory()
        => Given("a valid domain event dispatcher declaration", () => Compile("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.DomainEvents;
            using PatternKit.Generators.DomainEvents;
            namespace Demo;
            public abstract record OrderEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
            public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderEvent(EventId, OccurredAt);
            [GenerateDomainEventDispatcher(typeof(OrderEvent), FactoryName = "Build", DispatcherName = "order-events")]
            public static partial class OrderEventHandlers
            {
                [DomainEventHandler(typeof(OrderPlaced), 2)]
                private static ValueTask Audit(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
                [DomainEventHandler(typeof(OrderPlaced), 1)]
                private static ValueTask Project(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """))
        .Then("generated source creates ordered handlers", result =>
        {
            ScenarioExpect.Empty(result.Diagnostics);
            var source = ScenarioExpect.Single(result.GeneratedSources);
            ScenarioExpect.Contains("Build()", source);
            ScenarioExpect.Contains("Create(\"order-events\")", source);
            ScenarioExpect.Contains("Handle<global::Demo.OrderPlaced>(Project)", source);
            ScenarioExpect.Contains("Handle<global::Demo.OrderPlaced>(Audit)", source);
            ScenarioExpect.True(source.IndexOf("Project", StringComparison.Ordinal) < source.IndexOf("Audit", StringComparison.Ordinal));
        })
        .AssertPassed();

    [Scenario("Generator reports invalid domain event declarations")]
    [Theory]
    [InlineData("public static class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask Handle(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE001")]
    [InlineData("public static partial class OrderEventHandlers;", "PKDE002")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static string Handle(OrderPlaced domainEvent) => domainEvent.OrderId; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(NotAnOrderEvent), 1)] private static ValueTask Handle(NotAnOrderEvent domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE003")]
    [InlineData("public static partial class OrderEventHandlers { [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask One(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; [DomainEventHandler(typeof(OrderPlaced), 1)] private static ValueTask Two(OrderPlaced domainEvent, CancellationToken cancellationToken) => ValueTask.CompletedTask; }", "PKDE004")]
    public Task Generator_Reports_Invalid_Domain_Event_Declarations(string declaration, string diagnosticId)
        => Given("an invalid domain event dispatcher declaration", () => Compile($$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PatternKit.Application.DomainEvents;
            using PatternKit.Generators.DomainEvents;
            public abstract record OrderEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
            public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderEvent(EventId, OccurredAt);
            public sealed record NotAnOrderEvent(string Id);
            [GenerateDomainEventDispatcher(typeof(OrderEvent))]
            {{declaration}}
            """))
        .Then("the expected diagnostic is reported", result =>
            ScenarioExpect.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId))
        .AssertPassed();

    private static GeneratorResult Compile(string source)
    {
        var compilation = RoslynTestHelpers.CreateCompilation(
            source,
            "DomainEventDispatcherGeneratorTests",
            extra: MetadataReference.CreateFromFile(typeof(DomainEventDispatcher<>).Assembly.Location));
        _ = RoslynTestHelpers.Run(compilation, new DomainEventDispatcherGenerator(), out var run, out _);
        var result = run.Results.Single();
        return new GeneratorResult(
            result.Diagnostics.ToArray(),
            result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray());
    }

    private sealed record GeneratorResult(IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<string> GeneratedSources);
}
